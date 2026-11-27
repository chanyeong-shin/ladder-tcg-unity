using UnityEngine;
using UnityEngine.UI;

namespace Breach.UI
{
    // Title B, selected 2026-11-21: "새안 1 · 가장자리에서 경계로 덮기", glitch 보통, speed 1.
    // Line-for-line port of the SVG timeline in 조사_시안/2026-11-21_시안_11주차_연출모음.html (tab 1, mode "edge").
    // Coordinates are the SVG's 1280 x 720, y down. GlitchOnly draws the overlay group that sits above the logo.
    public sealed class TitleBBackdrop : MaskableGraphic
    {
        public float TimeValue=5; public bool GlitchOnly;
        public const float ContactTime=.6f, CoverStart=.75f, CoverEnd=2.05f;
        const int Bands=24; const float BandHeight=720f/Bands;
        Vector2 shift;
        static Color Hex(uint rgb,float a=1) => new Color(((rgb>>16)&255)/255f,((rgb>>8)&255)/255f,(rgb&255)/255f,a);
        static float Clamp(float v) => Mathf.Clamp01(v);
        static float Ease(float v){v=Clamp(v);return 1-Mathf.Pow(1-v,3);}
        static float EaseIO(float v){v=Clamp(v);return v<.5f?4*v*v*v:1-Mathf.Pow(-2*v+2,3)/2;}
        // Same deterministic noise as the HTML: the same time gives the same shape (rewind, skip).
        public static float Rnd(float a,float b=0,float c=0){double x=System.Math.Sin(a*127.1+b*311.7+c*74.7)*43758.5453;return (float)(x-System.Math.Floor(x));}
        static int Round(float v) => Mathf.FloorToInt(v+.5f);
        static float SeamX(float y) => 790-y/2.4f; // same line as the clip test x + y/2.4 = 790 (300/720 = 1/2.4)
        const float LineOffset=9, Inset=LineOffset+2.5f; // seam line distance from the field seam; traces stop 1 px inside their line

        void Poly(VertexHelper vh,Vector2[] p,Color[] colors)
        {
            int first=vh.currentVertCount;for(int i=0;i<p.Length;i++)vh.AddVert(new Vector3(p[i].x+shift.x,-(p[i].y+shift.y)),colors[i],Vector2.zero);
            for(int i=1;i<p.Length-1;i++)vh.AddTriangle(first,first+i,first+i+1);
        }
        void Box(VertexHelper vh,float x,float y,float w,float h,Color c)
        {
            float x0=Mathf.Max(0,x),x1=Mathf.Min(1280,x+w);if(x1<=x0||h<=0||c.a<=0)return;
            Poly(vh,new[]{new Vector2(x0,y),new Vector2(x1,y),new Vector2(x1,y+h),new Vector2(x0,y+h)},new[]{c,c,c,c});
        }
        void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color color)
        {
            if((b-a).sqrMagnitude<.00001f||color.a<=0)return;var n=new Vector2(-(b-a).y,(b-a).x).normalized*width/2;
            Poly(vh,new[]{a+n,b+n,b-n,a-n},new[]{color,color,color,color});
        }
        static Color Field(bool cyan,float x,float alpha)
        {
            var c=cyan?Color.Lerp(Hex(0x155663),Hex(0x07141d),Clamp(x/790)):Color.Lerp(Hex(0x190e19),Hex(0x6c1d32),Clamp((x-490)/790));c.a=alpha;return c;
        }
        // Field area between a left edge and a right edge (each may slant from y0 to y1), with the SVG's horizontal gradient.
        void FieldShape(VertexHelper vh,bool cyan,float l0,float l1,float r0,float r1,float y0,float y1,float alpha)
        {
            for(float x=0;x<1280;x+=20){float x2=x+20;
                float a0=Mathf.Clamp(l0,x,x2),a1=Mathf.Clamp(l1,x,x2),b0=Mathf.Clamp(r0,x,x2),b1=Mathf.Clamp(r1,x,x2);
                if(b0-a0<=.01f&&b1-a1<=.01f)continue;
                Poly(vh,new[]{new Vector2(a0,y0),new Vector2(b0,y0),new Vector2(b1,y1),new Vector2(a1,y1)},new[]{Field(cyan,a0,alpha),Field(cyan,b0,alpha),Field(cyan,b1,alpha),Field(cyan,a1,alpha)});
            }
        }
        // SVG radialGradient t-flare: #fff, .12 #fff1f7, .35 #ff526b at .8, 1 #ff526b at 0.
        void Flare(VertexHelper vh,Vector2 c,float rx,float ry,float alpha)
        {
            if(alpha<=0)return;float[] stops={0,.12f,.35f,1};Color[] inks={Color.white,Hex(0xfff1f7),Hex(0xff526b,.8f),Hex(0xff526b,0)};
            int seg=40,start=vh.currentVertCount;var center=inks[0];center.a*=alpha;vh.AddVert(new Vector3(c.x+shift.x,-(c.y+shift.y)),center,Vector2.zero);
            for(int r=1;r<stops.Length;r++)for(int i=0;i<seg;i++){float a=i*Mathf.PI*2/seg;var ink=inks[r];ink.a*=alpha;vh.AddVert(new Vector3(c.x+shift.x+Mathf.Cos(a)*rx*stops[r],-(c.y+shift.y+Mathf.Sin(a)*ry*stops[r])),ink,Vector2.zero);}
            for(int i=0;i<seg;i++)vh.AddTriangle(start,start+1+i,start+1+(i+1)%seg);
            for(int r=1;r<stops.Length-1;r++){int inner=start+1+(r-1)*seg,outer=inner+seg;for(int i=0;i<seg;i++){int j=(i+1)%seg;vh.AddTriangle(inner+i,outer+i,outer+j);vh.AddTriangle(inner+i,outer+j,inner+j);}}
        }
        void Ellipse(VertexHelper vh,Vector2 c,float rx,float ry,float width,Color ink)
        {
            for(int i=0;i<64;i++){float a=i*Mathf.PI/32,b=(i+1)*Mathf.PI/32;Line(vh,c+new Vector2(Mathf.Cos(a)*rx,Mathf.Sin(a)*ry),c+new Vector2(Mathf.Cos(b)*rx,Mathf.Sin(b)*ry),width,ink);}
        }
        // glitchLevel(t): strongest at contact, weaker as the fields cover, one more kick, a short one at the title.
        public static float GlitchLevel(float t)
        {
            if(t<ContactTime)return 0;float d=t-ContactTime;
            float g=d<.12f?1:Mathf.Max(0,1-(d-.12f)/1.35f)*.75f;
            if(t>1.18f&&t<1.30f)g=Mathf.Max(g,.7f);
            if(t>2.90f&&t<3.08f)g=Mathf.Max(g,.35f);
            return g;
        }
        void Glitch(VertexHelper vh,float t,float g,int tick)
        {
            if(g<=0)return;
            int n=Round(10+22*g);
            for(int k=0;k<n;k++){
                float y=Rnd(tick,k,3)*720,h=2+Rnd(tick,k,4)*16*g,x=Rnd(tick,k,5)*1280-200,w=60+Rnd(tick,k,6)*620,c=Rnd(tick,k,7);
                var fill=c<.38f?Hex(0x57dcf4):c<.76f?Hex(0xff526b):c<.9f?Color.white:Color.black;fill.a=(.18f+Rnd(tick,k,8)*.6f)*Mathf.Min(1,g);
                Box(vh,x,y,w,h,fill);
            }
            float d=t-ContactTime,core=Clamp(1-d/.5f);
            if(core>0){float off=6+18*g;
                Box(vh,648-420-off,343,840,4,Hex(0x57dcf4,.8f*core));Box(vh,648-420+off,347,840,4,Hex(0xff526b,.8f*core));Box(vh,648-300,344,600,3,new Color(1,1,1,core));}
            for(int k=0;k<Round(4*g);k++)Box(vh,0,Rnd(tick,k,9)*720,1280,3+Rnd(tick,k,10)*10,new Color(0,0,0,.85f));
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();float t=TimeValue;int tick=Mathf.FloorToInt(t*24);float g=GlitchLevel(t);shift=Vector2.zero;
            if(GlitchOnly){Glitch(vh,t,g,tick);return;}
            Box(vh,0,0,1280,720,Color.black);
            // #world shake
            float shake=g*6;shift=new Vector2((Rnd(tick,11)-.5f)*2*shake,(Rnd(tick,12)-.5f)*shake);
            var cyan=Hex(0x57dcf4);var red=Hex(0xff526b);
            // Fields: each band covers from its own screen edge toward the diagonal seam, its front edge glitching.
            if(t>=CoverStart){
                float p=Clamp((t-CoverStart)/(CoverEnd-CoverStart)),amp=(10+60*g)*(1-p*.6f)*(p<1?1:0);
                float flick=g>0?.78f+.22f*Rnd(tick,99):1;
                for(int j=0;j<Bands;j++){
                    float y0=j*BandHeight,y1=y0+BandHeight,sx=SeamX((y0+y1)/2);
                    float lag=Rnd(j,7)*.22f,pj=Clamp((p-lag)/(1-lag)); // 11-27: was /(1-lag*.6) which never reached 1 at p=1, leaving stepped black gaps at the seam
                    float jit=(Rnd(j,tick,1)-.5f)*2*amp,jit2=(Rnd(j,tick,2)-.5f)*2*amp;
                    float cR=Mathf.Max(0,Mathf.Min(sx,sx*EaseIO(pj)+jit)),rL=Mathf.Min(1280,Mathf.Max(sx,1280-(1280-sx)*EaseIO(pj)+jit2));
                    float sxa=SeamX(y0),sxb=SeamX(y1);
                    if(pj>=1&&amp<.5f){FieldShape(vh,true,0,0,sxa,sxb,y0,y1,flick);FieldShape(vh,false,sxa,sxb,1280,1280,y0,y1,flick);continue;}
                    if(cR>.5f)FieldShape(vh,true,0,0,cR,cR,y0,y1,flick);
                    if(1280-rL>.5f)FieldShape(vh,false,rL,rL,1280,1280,y0,y1,flick);
                }
            }
            // Circuits: same paths as 11-09, start 2.1 s + 0.075 s per row + 0.05 s for the red side, 1.1 s each, clipped to their field.
            for(int side=0;side<2;side++)for(int i=0;i<13;i++){
                float y=36+i*55,end=side==1?665-(i%3)*43:620+(i%3)*45,bend=side==1?1030-(i%4)*60:250+(i%4)*60;
                var pts=new[]{new Vector2(side==1?1360:-80,y),new Vector2(bend,y),new Vector2(bend+(side==1?-65:65),y+30),new Vector2(end,y+30)};
                float pr=Ease((t-(2.1f+i*.075f+side*.05f))/1.1f);if(pr<=0)continue;
                float total=0;for(int k=1;k<4;k++)total+=Vector2.Distance(pts[k-1],pts[k]);float remaining=total*pr;Vector2 head=pts[0];
                Color ink=side==0?cyan:red;var stroke=ink;stroke.a=.24f;
                for(int k=1;k<4&&remaining>0;k++){
                    Vector2 a=pts[k-1],b=Vector2.Lerp(a,pts[k],Mathf.Clamp01(remaining/Vector2.Distance(a,pts[k])));remaining-=Vector2.Distance(a,pts[k]);head=b;
                    // 11-27 fix (user: title circuits slightly leave their area): clip each trace to the inside of its own seam line
                    // (seam lines are drawn parallel to SeamX at -/+LineOffset), not to the field boundary itself.
                    float clip=side==0?Inset:-Inset;
                    float da=a.x+a.y/2.4f-790+clip,db=b.x+b.y/2.4f-790+clip;bool va=side==0?da<=0:da>=0,vb=side==0?db<=0:db>=0;
                    if(!va&&!vb)continue;if(va!=vb){var cross=Vector2.Lerp(a,b,da/(da-db));if(va)b=cross;else a=cross;}
                    a.x=Mathf.Clamp(a.x,0,1280);b.x=Mathf.Clamp(b.x,0,1280);a.y=Mathf.Clamp(a.y,0,720);b.y=Mathf.Clamp(b.y,0,720);
                    Line(vh,a,b,i%4==0?2:1,stroke);
                }
                float headSide=head.x+head.y/2.4f-790+(side==0?Inset+3:-Inset-3);
                if(side==0?headSide<=0:headSide>=0){var dot=ink;dot.a=pr<1?.9f:.45f;var ring=new Vector2[16];var colors=new Color[16];for(int n=0;n<16;n++){float angle=n*Mathf.PI/8;ring[n]=head+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*3;colors[n]=dot;}Poly(vh,ring,colors);}
            }
            // Seam lines light up when the fields meet.
            float seam=Ease((t-CoverEnd)/.25f);
            // 11-27: the old lines (782,-10)-(482,730) and (800,-10)-(500,730) were not parallel to the field seam (790 - y/2.4),
            // so near the bottom the red field and red traces reached past the red line. Draw them parallel at -/+LineOffset.
            Line(vh,new Vector2(SeamX(-10)-LineOffset,-10),new Vector2(SeamX(730)-LineOffset,730),3,new Color(cyan.r,cyan.g,cyan.b,seam));
            Line(vh,new Vector2(SeamX(-10)+LineOffset,-10),new Vector2(SeamX(730)+LineOffset,730),3,new Color(red.r,red.g,red.b,seam));
            // Inbound signals accelerate from both edges and vanish at contact.
            if(t<ContactTime){
                float acc=Mathf.Pow(Clamp(t/ContactTime),3),cx=-50+698*acc,rx=1330-682*acc;
                Line(vh,new Vector2(cx-170,345),new Vector2(cx,345),14,Hex(0x91efff,.25f));Line(vh,new Vector2(cx-170,345),new Vector2(cx,345),4,Hex(0x91efff));
                Line(vh,new Vector2(rx+170,345),new Vector2(rx,345),14,Hex(0xff8295,.25f));Line(vh,new Vector2(rx+170,345),new Vector2(rx,345),4,Hex(0xff8295));
            }
            // Contact flare and ring, 0.42 s.
            float dt=t-ContactTime;
            if(dt>=0&&dt<.42f){float k=Clamp(dt/.42f),burst=(1-k)*(1-k);var center=new Vector2(648,345);
                Flare(vh,center,430,22,burst);Flare(vh,center,26,100,burst);Ellipse(vh,center,22+180*Ease(k),22+130*Ease(k),2,Hex(0xffb4c0,burst));}
        }
    }
}
