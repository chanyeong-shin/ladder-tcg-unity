// Week 12 draw lock (12_drawlimit.js "lock"): CSS filter grayscale(_Gray) brightness(_Bright) on a UI image.
// Both are linear color matrices in sRGB values (the project is in Gamma space), so per-graphic application equals the CSS filter.
Shader "Breach/UI Gray" {
 Properties { [PerRendererData] _MainTex ("Texture", 2D) = "white" {} _Gray ("Gray", Float) = 0 _Bright ("Bright", Float) = 1 }
 SubShader {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
  Cull Off ZWrite Off ZTest [unity_GUIZTestMode] Blend SrcAlpha OneMinusSrcAlpha
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   sampler2D _MainTex; float4 _MainTex_ST; float _Gray, _Bright;
   struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
   struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
   v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = TRANSFORM_TEX(v.uv, _MainTex); o.color = v.color; return o; }
   float4 frag (v2f i) : SV_Target {
    float4 t = tex2D(_MainTex, i.uv) * i.color; float l = dot(t.rgb, float3(.2126, .7152, .0722));
    return float4(saturate(lerp(t.rgb, l.xxx, _Gray) * _Bright), t.a);
   }
   ENDCG
  }
 }
}
