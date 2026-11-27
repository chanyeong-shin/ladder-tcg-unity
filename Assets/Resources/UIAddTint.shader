// Week 12 glitch (01_activate.js): adds texture x vertex color x vertex alpha (Blend One One). Used for the s^2 R term of the
// HTML's 'screen' composite of the red/cyan board copies (see ActivateFx.Add).
Shader "Breach/UI Add Tint" {
 Properties { [PerRendererData] _MainTex ("Texture", 2D) = "white" {} }
 SubShader {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
  Cull Off ZWrite Off ZTest [unity_GUIZTestMode] Blend One One
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   #include "UnityUI.cginc"
   #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
   sampler2D _MainTex; float4 _MainTex_ST; float4 _ClipRect;
   struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
   struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; float4 world : TEXCOORD1; };
   v2f vert (appdata v) { v2f o; o.world = v.vertex; o.pos = UnityObjectToClipPos(v.vertex); o.uv = TRANSFORM_TEX(v.uv, _MainTex); o.color = v.color; return o; }
   float4 frag (v2f i) : SV_Target {
    float4 t = tex2D(_MainTex, i.uv); float k = i.color.a;
    #ifdef UNITY_UI_CLIP_RECT
    k *= UnityGet2DClipping(i.world.xy, _ClipRect);
    #endif
    return float4(t.rgb * i.color.rgb * k, 1);
   }
   ENDCG
  }
 }
}
