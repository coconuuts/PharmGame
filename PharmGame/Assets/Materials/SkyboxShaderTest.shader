Shader "Skybox/NightDay HDRI" // Changed shader name to avoid conflict and indicate HDRI support
{
    Properties
    {
        // Changed property type from 2D to Cube for Cubemap textures
        _Texture1("Day Skybox (HDRI)", Cube) = "" {} 
        _Texture2("Night Skybox (HDRI)", Cube) = "" {}
        _Blend("Blend", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Background" "Queue"="Background" } // Standard skybox tags
        Cull Off // Skyboxes should not be culled
        ZWrite Off // Skyboxes should not write to the depth buffer
        ZTest LEqual // Skyboxes should render behind everything else

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                // TEXCOORD0 will now hold the 3D direction vector for cubemap sampling
                float3 texcoord : TEXCOORD0; 
                float4 vertex : SV_POSITION;
            };

            // Changed sampler type from sampler2D to samplerCUBE
            samplerCUBE _Texture1; 
            samplerCUBE _Texture2;
            float _Blend;

            v2f vert(appdata v)
            {
                v2f o;
                // For a skybox, the vertex position is treated as a direction vector from the origin.
                // We normalize it to ensure it's a unit vector suitable for cubemap sampling.
                o.texcoord = normalize(v.vertex.xyz); 
                // Transform the vertex to clip space for rendering
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            // Removed the ToRadialCoords function as it's not needed for cubemap sampling

            fixed4 frag(v2f i) : SV_Target
            {
                // Sample the cubemaps directly using the 3D direction vector
                fixed4 tex1 = texCUBE(_Texture1, i.texcoord);
                fixed4 tex2 = texCUBE(_Texture2, i.texcoord);
                
                // Linearly interpolate between the two sampled colors based on the blend factor
                return lerp(tex1, tex2, _Blend);
            }
            ENDCG
        }
    }
    FallBack "Skybox/Panoramic" // Optional: Fallback shader if this one fails
}