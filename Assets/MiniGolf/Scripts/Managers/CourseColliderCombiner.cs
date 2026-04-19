using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace MiniGolf
{
    public class CourseColliderCombiner : MonoBehaviour
    {
        [SerializeField]
        private PhysicsMaterial coursePhysicsMaterial;

        void Awake()
        {
            GameManager.Instance.OnLoadHole += OnLoadHole;
        }

        // Each time we load up a new hole, combine the colliders into one.
        void OnLoadHole(CourseData course, int hole)
        {
            CombineColliders(GameManager.Instance.CurrentHoleObject);
        }

        public void CombineColliders(GameObject holeObject)
        {
            // Get all the children mesh filters from the hole object.
            MeshFilter[] rawMeshFilters = holeObject.GetComponentsInChildren<MeshFilter>();
            List<MeshFilter> meshFilters = new List<MeshFilter>();

            // Remove any mesh filters that aren't static/don't already have a mesh collider.
            foreach(MeshFilter mf in rawMeshFilters)
            {
                if(!mf.gameObject.isStatic)
                    continue;

                if(!mf.GetComponent<MeshCollider>())
                    continue;

                meshFilters.Add(mf);
            }

            // Gather all the meshes needed to combine.
            CombineInstance[] combine = new CombineInstance[meshFilters.Count];

            for(int i = 0; i < meshFilters.Count; i++)
            {
                combine[i].mesh = meshFilters[i].sharedMesh;
                combine[i].transform = meshFilters[i].transform.localToWorldMatrix;

                // Disable the old mesh collider components as we don't need them anymore.
                if(meshFilters[i].TryGetComponent<MeshCollider>(out var mc))
                {
                    mc.enabled = false;
                }
            }

            // Create the mesh by combining all existing meshes.
            Mesh mesh = new Mesh();
            mesh.CombineMeshes(combine);

            // Weld overlapping vertices together.
            Vector3[] oldVertices = mesh.vertices;
            int[] oldTriangles = mesh.triangles;

            List<Vector3> newVertices = new List<Vector3>();
            List<int> newTriangles = new List<int>();
            Dictionary<int, int> oldToNewVertexMap = new Dictionary<int, int>();

            for(int i = 0; i < oldVertices.Length; i++)
            {
                Vector3 v = oldVertices[i];
                bool found = false;

                for(int x = 0; x < newVertices.Count; x++)
                {
                    if((newVertices[x] - v).magnitude <= 0.01f)
                    {
                        oldToNewVertexMap[i] = x;
                        found = true;
                        break;
                    }
                }

                if(!found)
                {
                    oldToNewVertexMap[i] = newVertices.Count;
                    newVertices.Add(v);
                }
            }

            for(int i = 0; i < oldTriangles.Length; i++)
            {
                newTriangles.Add(oldToNewVertexMap[oldTriangles[i]]);
            }

            // Create a new mesh and assign the verts and tris to it. 
            Mesh newMesh = new Mesh();
            newMesh.SetVertices(newVertices.ToArray());
            newMesh.SetTriangles(newTriangles.ToArray(), 0);

            // Create the new MeshCollider component.
            MeshCollider newMeshCollider = holeObject.AddComponent<MeshCollider>();
            newMeshCollider.sharedMesh = newMesh;
            newMeshCollider.material = coursePhysicsMaterial;
        }
    }
}