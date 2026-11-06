Shader "Breach/CRT Overlay" {
 Properties { [PerRendererData] _MainTex("UI Texture",2D)="white" {} _Strength("Strength",Range(0,1))=.45 _Burst("Turn burst",Range(0,1))=0 }
 SubShader {
  Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"}
  Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
  Blend SrcAlpha OneMinusSrcAlpha
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
   struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;};
   float _Strength, _Burst;
   v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
   float random(float2 p){return frac(sin(dot(p,float2(12.9898,78.233)))*43758.5453);}
   fixed4 frag(v2f i):SV_Target {
    float2 uv=i.uv;float clock=floor(_Time.y*12);
    float grain=random(floor(uv*float2(960,540))+clock);
    float scan=pow(.5+.5*cos(uv.y*540*6.283185),5);
    float2 edge=abs(uv-.5)*2;float vignette=pow(max(edge.x,edge.y),5);
    float band=1-smoothstep(0,.018,abs(uv.y-frac(_Time.y*.045)));
    float tracking=band*(.3+.7*grain);
    float alpha=(scan*.13+grain*.025+vignette*.18+tracking*.05)*_Strength;
    float fast=floor(_Time.y*35);
    float lines=random(float2(floor(uv.y*210),fast));
    float tear=step(.94,lines)*(.3+.7*random(float2(floor(uv.x*90),fast)));
    float staticNoise=step(.965,random(floor(uv*float2(320,180))+fast));
    float rolling=1-smoothstep(0,.025,abs(uv.y-frac(_Time.y*1.7)));
    float burst=(tear*.55+staticNoise*.65+rolling*.22+scan*.2)*_Burst;
    float3 tint=lerp(float3(.015,.025,.035),float3(.32,.5,.52),tracking);
    float3 signal=lerp(float3(.9,.15,.25),float3(.35,.9,1),step(.5,lines));
    tint=lerp(tint,lerp(signal,float3(.9,.95,1),staticNoise),saturate(burst*3));
    return fixed4(tint,saturate(alpha+burst));
   }
   ENDCG
  }
 }
}
