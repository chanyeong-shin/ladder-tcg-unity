using UnityEngine;
using UnityEngine.UI;

namespace Breach.UI
{
    public sealed class BoardGradient : MaskableGraphic
    {
        public Color BaseColor=new Color(16/255f,38/255f,48/255f);
        public Color LightColor=new Color(80/255f,143/255f,160/255f);
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();const int cols=64,rows=16;var size=rectTransform.rect.size;
            for(int y=0;y<=rows;y++)for(int x=0;x<=cols;x++){
                float u=x/(float)cols,v=y/(float)rows;
                float a=.304f*Mathf.Exp(-Mathf.Pow((u-.5f)/.28f,2))*Mathf.SmoothStep(.45f,1,Mathf.Min(v,1-v)*size.y/24);
                vh.AddVert(new Vector3(u*size.x,-v*size.y),Color.Lerp(BaseColor,LightColor,a),Vector2.zero);
                if(x<cols&&y<rows){int i=y*(cols+1)+x;vh.AddTriangle(i,i+cols+1,i+1);vh.AddTriangle(i+1,i+cols+1,i+cols+2);}
            }
        }
    }
}
