using UnityEngine;
using UnityEngine.UI;

namespace Breach.UI
{
    public sealed class TitleBBackdrop : MaskableGraphic
    {
        public float TimeValue=4.5f;
        public const float ContactTime=.72f, ReleaseTime=.805f;
        static Color Hex(uint rgb,float a=1) => new Color(((rgb>>16)&255)/255f,((rgb>>8)&255)/255f,(rgb&255)/255f,a);
        void Poly(VertexHelper vh,Vector2[] p,Color[] colors)
        {
            int first=vh.currentVertCount;for(int i=0;i<p.Length;i++)vh.AddVert(new Vector3(p[i].x,-p[i].y),colors[i],Vector2.zero);
            for(int i=1;i<p.Length-1;i++)vh.AddTriangle(first,first+i,first+i+1);
        }
        void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color color)
        {
            if((b-a).sqrMagnitude<.00001f)return;var n=new Vector2(-(b-a).y,(b-a).x).normalized*width/2;
            Poly(vh,new[]{a+n,b+n,b-n,a-n},new[]{color,color,color,color});
        }
        void Glow(VertexHelper vh,Vector2 p,float w,float h,Color ink,float alpha)
        {
            if(alpha<=0)return;
            const int rings=16;for(int i=rings;i>0;i--){float r=i/(float)rings;float a=alpha*.12f*(1-r*.7f);var c=new Color(ink.r,ink.g,ink.b,a);Poly(vh,new[]{p+new Vector2(-w*r,0),p+new Vector2(0,-h*r),p+new Vector2(w*r,0),p+new Vector2(0,h*r)},new[]{c,c,c,c});}
            Line(vh,p-new Vector2(w*.75f,0),p+new Vector2(w*.75f,0),1.5f,new Color(1,1,1,alpha));
        }
        void SoftCore(VertexHelper vh,Vector2 p,float rx,float ry,Color ink,float alpha)
        {
            int start=vh.currentVertCount;vh.AddVert(new Vector3(p.x,-p.y),new Color(ink.r,ink.g,ink.b,alpha),Vector2.zero);
            for(int i=0;i<=48;i++){float a=i*Mathf.PI/24;vh.AddVert(new Vector3(p.x+Mathf.Cos(a)*rx,-p.y-Mathf.Sin(a)*ry),new Color(ink.r,ink.g,ink.b,0),Vector2.zero);if(i>0)vh.AddTriangle(start,start+i,start+i+1);}
        }
        void ShockRing(VertexHelper vh,Vector2 center,float rx,float ry,float alpha,float weight)
        {
            for(int i=0;i<72;i++){
                float a=i*Mathf.PI/36,b=(i+1)*Mathf.PI/36;
                Color ink=Mathf.Cos(a)<0?Hex(0x57dcf4,alpha):Hex(0xff526b,alpha);
                Line(vh,center+new Vector2(Mathf.Cos(a)*rx,Mathf.Sin(a)*ry),center+new Vector2(Mathf.Cos(b)*rx,Mathf.Sin(b)*ry),weight,ink);
            }
        }
        void Collision(VertexHelper vh,Color cyan,Color red)
        {
            var center=new Vector2(640,345);
            float dark=.55f*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(ReleaseTime,1.65f,TimeValue)));
            if(dark>0){var shade=new Color(0,0,0,dark);Poly(vh,new[]{Vector2.zero,new Vector2(1280,0),new Vector2(1280,720),new Vector2(0,720)},new[]{shade,shade,shade,shade});}
            if(TimeValue<ReleaseTime){
                float progress=Mathf.Pow(Mathf.Clamp01(TimeValue/ContactTime),2.8f);
                float squeeze=Mathf.InverseLerp(ContactTime,ReleaseTime,TimeValue);
                for(int side=0;side<2;side++){
                    float direction=side==0?1:-1;Color ink=side==0?cyan:red;
                    Vector2 p=new Vector2(Mathf.Lerp(side==0?-120:1400,640-direction*13,progress),345);
                    for(int n=0;n<5;n++){
                        float lift=(n-2)*8*(1-progress*.7f),length=90+n*33;
                        Line(vh,p+new Vector2(-direction*length,lift),p+new Vector2(-direction*8,lift*.15f),3-n*.4f,new Color(ink.r,ink.g,ink.b,.65f-n*.09f));
                    }
                    SoftCore(vh,p,90-55*squeeze,36+30*squeeze,ink,.9f);
                    Glow(vh,p,110-65*squeeze,18+16*squeeze,ink,1);
                    SoftCore(vh,p,14-7*squeeze,10+12*squeeze,Color.white,1);
                }
                if(TimeValue>=ContactTime){SoftCore(vh,center,36,70,Color.white,.9f);ShockRing(vh,center,28,55,.85f,2.5f);}
                return;
            }
            float age=TimeValue-ReleaseTime;if(age>=.95f)return;
            float decay=Mathf.Pow(1-Mathf.Clamp01(age/.72f),2),travel=1-Mathf.Pow(1-Mathf.Clamp01(age/.7f),3);
            // Bright contact core, long horizontal release and distinct red/blue shoulders.
            SoftCore(vh,center+new Vector2(-28-95*travel,0),170,92,cyan,decay*.9f);
            SoftCore(vh,center+new Vector2(28+95*travel,0),170,92,red,decay*.9f);
            Glow(vh,center,590,49,Color.white,decay);
            Glow(vh,center,48,205,Color.white,decay*.8f);
            SoftCore(vh,center,85,72,Color.white,decay);
            ShockRing(vh,center,35+535*travel,30+235*travel,Mathf.Pow(1-travel,.6f)*.9f,3*(1-travel)+1);
            if(age>.07f){float wave=1-Mathf.Pow(1-Mathf.Clamp01((age-.07f)/.78f),3);ShockRing(vh,center,30+420*wave,20+175*wave,(1-wave)*.55f,1.5f);}
            for(int i=0;i<36;i++){
                float angle=i*Mathf.PI/18+.13f;var direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                float distance=35+(160+i%5*48)*travel;
                var ink=direction.x<0?cyan:red;ink.a=1-Mathf.Clamp01(age/(.42f+i%4*.11f));
                Vector2 p=center+new Vector2(direction.x*distance*1.35f,direction.y*distance*.7f);
                Line(vh,p,p+direction*(8+35*(1-travel)),i%4==0?3:1.3f,ink);
            }
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var cyan=Hex(0x57dcf4);var red=Hex(0xff526b);
            // Horizontal gradient clipped by the B diagonal, matching the source SVG.
            for(int i=0;i<128;i++){
                float x=i*10,x2=x+10;float yl=Mathf.Clamp((790-x)*2.4f,0,720),yr=Mathf.Clamp((790-x2)*2.4f,0,720);
                Color l=Color.Lerp(Hex(0x155663),Hex(0x07141d),Mathf.Clamp01(x/790)),r=Color.Lerp(Hex(0x155663),Hex(0x07141d),Mathf.Clamp01(x2/790));
                Poly(vh,new[]{new Vector2(x,0),new Vector2(x2,0),new Vector2(x2,yr),new Vector2(x,yl)},new[]{l,r,r,l});
                l=Color.Lerp(Hex(0x190e19),Hex(0x6c1d32),Mathf.Clamp01((x-490)/790));r=Color.Lerp(Hex(0x190e19),Hex(0x6c1d32),Mathf.Clamp01((x2-490)/790));
                Poly(vh,new[]{new Vector2(x,yl),new Vector2(x2,yr),new Vector2(x2,720),new Vector2(x,720)},new[]{l,r,r,l});
            }
            float reveal=Mathf.SmoothStep(0,1,Mathf.Clamp01((TimeValue-.75f)/1.1f));
            Line(vh,new Vector2(782,-10),new Vector2(482,730),3,new Color(cyan.r,cyan.g,cyan.b,reveal));
            Line(vh,new Vector2(800,-10),new Vector2(500,730),3,new Color(red.r,red.g,red.b,reveal));
            for(int side=0;side<2;side++)for(int i=0;i<13;i++){
                float y=36+i*55;float e=side==0?-80:1360,bend=side==0?250+i%4*60:1030-i%4*60,inner=side==0?620+i%3*45:665-i%3*43;
                var p=new[]{new Vector2(e,y),new Vector2(bend,y),new Vector2(bend+(side==0?65:-65),y+30),new Vector2(inner,y+30)};
                float progress=1-Mathf.Pow(1-Mathf.Clamp01((TimeValue-.85f-i*.075f)/1.1f),3);
                float total=0;for(int k=1;k<4;k++)total+=Vector2.Distance(p[k-1],p[k]);float remaining=total*progress;
                Color ink=side==0?cyan:red;ink.a=.24f;
                for(int k=1;k<4&&remaining>0;k++){
                    Vector2 a=p[k-1],b=Vector2.Lerp(a,p[k],Mathf.Clamp01(remaining/Vector2.Distance(a,p[k])));remaining-=Vector2.Distance(a,p[k]);
                    // Clip to x + y/2.4 = 790 and the canvas viewport.
                    float da=a.x+a.y/2.4f-790,db=b.x+b.y/2.4f-790;bool va=side==0?da<=0:da>=0,vb=side==0?db<=0:db>=0;
                    if(!va&&!vb)continue;if(va!=vb){var cross=Vector2.Lerp(a,b,da/(da-db));if(va)b=cross;else a=cross;}
                    a.x=Mathf.Clamp(a.x,0,1280);b.x=Mathf.Clamp(b.x,0,1280);a.y=Mathf.Clamp(a.y,0,720);b.y=Mathf.Clamp(b.y,0,720);
                    Line(vh,a,b,i%4==0?2:1,ink);
                }
                float sideValue=p[3].x+p[3].y/2.4f-790;
                if(progress>=.999f&&p[3].y<717&&(side==0?sideValue<=0:sideValue>=0)){
                    var ring=new Vector2[16];var colors=new Color[16];ink.a=.45f;
                    for(int n=0;n<16;n++){float angle=n*Mathf.PI/8;ring[n]=p[3]+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*3;colors[n]=ink;}Poly(vh,ring,colors);
                }
            }
            Collision(vh,cyan,red);
        }
    }
}
