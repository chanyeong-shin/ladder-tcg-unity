// CSS mix-blend-mode: screen for the VHS color-split layers (HTML tab 5). Vertex color tints and scales the copy.
Shader "Breach/UI Screen Blend" {
 Properties { [PerRendererData] _MainTex ("Texture", 2D) = "white" {} }
 SubShader {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
  Cull Off ZWrite Off ZTest [unity_GUIZTestMode] Blend OneMinusDstColor One
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   sampler2D _MainTex; float4 _MainTex_ST;
   struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
   struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
   v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = TRANSFORM_TEX(v.uv, _MainTex); o.color = v.color; return o; }
   // sepia(1) saturate(6) hue-rotate(): a single-hue copy driven by luminance
   float4 frag (v2f i) : SV_Target { float4 t = tex2D(_MainTex, i.uv); float l = dot(t.rgb, float3(.299, .587, .114)); return float4(l * i.color.rgb * i.color.a, 1); }
   ENDCG
  }
 }
}
