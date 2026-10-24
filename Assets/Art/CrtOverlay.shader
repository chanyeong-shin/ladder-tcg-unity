Shader "Breach/CRT Overlay" {
 Properties { _Strength("Strength",Range(0,1))=.45 }
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
   float _Strength;
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
    return fixed4(lerp(float3(.015,.025,.035),float3(.32,.5,.52),tracking),alpha);
   }
   ENDCG
  }
 }
}
