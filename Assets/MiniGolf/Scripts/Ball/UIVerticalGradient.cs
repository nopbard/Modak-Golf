using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MiniGolf
{
    // UI Graphic의 버텍스 컬러를 Y좌표에 따라 top→bottom 선형 보간으로 덮어쓰는 메쉬 이펙트.
    // 9-slice Image에도 동작 (버텍스 위치로 보간하므로 슬라이스가 많아도 자연스러움).
    [RequireComponent(typeof(Graphic))]
    public class UIVerticalGradient : BaseMeshEffect
    {
        public Color topColor = Color.white;
        public Color bottomColor = new Color(1f, 1f, 1f, 0.2f);

        public override void ModifyMesh(VertexHelper vh)
        {
            if(!IsActive() || vh.currentVertCount == 0)
                return;

            List<UIVertex> verts = new List<UIVertex>();
            vh.GetUIVertexStream(verts);

            float yMin = float.MaxValue, yMax = float.MinValue;
            for(int i = 0; i < verts.Count; i++)
            {
                float y = verts[i].position.y;
                if(y < yMin) yMin = y;
                if(y > yMax) yMax = y;
            }
            float range = Mathf.Max(0.0001f, yMax - yMin);

            for(int i = 0; i < verts.Count; i++)
            {
                UIVertex v = verts[i];
                float t = (v.position.y - yMin) / range;
                v.color = Color.Lerp(bottomColor, topColor, t);
                verts[i] = v;
            }

            vh.Clear();
            vh.AddUIVertexTriangleStream(verts);
        }
    }
}
