using UnityEngine;

namespace MiniGolf
{
    // When a new hole is loaded, this script hides any obstacles that may be in the way.
    public class EnvironmentManager : MonoBehaviour
    {
        private GameObject[] pieces;

        [SerializeField]
        private CameraController cam;

        void Awake()
        {
            GameManager.Instance.OnLoadHole += (a, b) => FormPiecesAroundCourse();
        }

        // When we load a new hole prefab in, we don't want any of the trees clipping into it
        // We check to see if there are any pieces overlapping the course and if so, deactivate them
        void FormPiecesAroundCourse()
        {
            if(pieces == null)
                pieces = GameObject.FindGameObjectsWithTag("EnvironmentPiece");

            LayerMask pieceLayerMask = LayerMask.GetMask("Environment");

            // Re-enable all pieces.
            for(int i = 0; i < pieces.Length; i++)
            {
                pieces[i].SetActive(true);
            }

            GameObject holeObj = GameManager.Instance.CurrentHoleObject;
            Collider[] collBuffer = new Collider[20];

            // 1. Loop through each course part.
            // 2. Check to see if there are any environemnt pieces nearby - if so, disable the piece.
            for(int i = 0; i < holeObj.transform.childCount; i++)
            {
                Transform obj = holeObj.transform.GetChild(i);
                int numHits = Physics.OverlapSphereNonAlloc(obj.transform.position, 1.0f, collBuffer, pieceLayerMask);

                for(int y = 0; y < numHits; y++)
                {
                    collBuffer[y].gameObject.SetActive(false);
                }
            }
        }
    }
}