Shader "SLG_Scene/SLG_Standard_Build"
{
    Properties
    {
        [Header(MainInf)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 2
        [HideInInspector] _Mode ("__mode", Float) = 0.0
        [HideInInspector] _Cutoff ("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        _BaseColor ("主纹理颜色", Color) = (1,1,1,1)
        _MainTex ("主纹理", 2D) = "white" {}
        _MainAlpha ("主纹理透明度", Range(0, 1)) = 1
        _Normal ("法线信息", 2D) = "bump" {}
        _NormalScale ("法线强度", Range(0,1)) = 1
        [Space(20)]
        _MRA ("R(金属遮罩)G(粗糙度)B(暂定)A(AO)", 2D) = "white" {}
        _Emission ("自发光", 2D) = "white" {}
        [Space(10)]
        _Smoothness ("光滑强度", Range(0,1)) = 0
        _Metallic ("金属强度", Range(0,1)) = 0
        [Space(10)]
        _OcclusionStrength ("AO强度", Range(0, 2)) = 1
        [Space(10)]
        _HightlightColor ("高光颜色", Color) = (0,0,0,0)
        [Toggle(use_SelfLight)] use_SelfLight ("使用自发光", Int) = 0
        [HDR]_EmissionColor ("自发光颜色(A通道控制自发光强度)", Color) = (0,0,0,0)
        _EmissionIntensity ("自发光强度", float) = 0.2
        [Toggle(use_Control)] use_Control("开启自发光频闪", Int) = 0
        _EmissionSpeed ("自发光频闪速度", float) = 1
        [Space(20)]
        _CubeMap ("反射球", Cube) = "white" {}
        _CubeScale ("反射信息透明度", Range(0,1)) = 0.2
        [MaterialToggle]_CubeMapBlend("反射球叠加计算",float)=0
        [Header(Shadow)]
        [Toggle(use_LightMap)] use_LightMap ("是否使用关照贴图", Int) = 0
        _LightMapTex ("光照贴图", 2D) = "white" {}
        _LightMapIntensity ("光照贴图强度", Range(0,1)) = 1
        _GroundHeight ("阴影高度", float) = 0
        [Space(20)]
        [Header(Blend)]
        [Toggle(use_Blend)] use_Blend ("使用高度消隐", Int) = 0
        _Smooth ("中心消隐度", Range(0,1)) = 0.2
        _SmoothValue ("消隐值", Range(1, 10)) = 1
        _TerrainHighEnum ("0为从下往上，1为从上往下渐变", Range(0.0, 1.0)) = 0
		_highSmooth ("高度消隐", Range(-50, 20)) = 1
            
        [Toggle(BUILD_SELECT)] _Select("是否点选",float) = 0

        _LightDir("lD", Vector) = (1,1,1,1)
        _LightColor ("_LightColor", Color) = (1,1,1,1)
        _Intensity ("Intensity", float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 4
    }
    SubShader
    {
        Tags
        { 
            "RenderType"="Opaque"
            "RenderPipeline"="UniversalRenderPipeline"
        }
        ZTest[_ZTest]
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Assets/_Resources/Shader/HLSL/Standard_LightDepend.hlsl"
        TEXTURE2D(_MainTex);          SAMPLER(sampler_MainTex);
        TEXTURE2D(_Normal);           SAMPLER(sampler_Normal);
        TEXTURE2D(_MRA);              SAMPLER(sampler_MRA);
        TEXTURE2D(_Emission);          SAMPLER(sampler_Emission);
        TEXTURECUBE(_CubeMap);        SAMPLER(sampler_CubeMap);
        TEXTURE2D(_LightMapTex);      SAMPLER(sampler_LightMapTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST, _Normal_ST, _MRA_ST, _Emission_ST, _CubeMap_ST, _LightMapTex_ST;
            float4 _BaseColor, _HightlightColor, _EmissionColor;
            float _MainAlpha, _NormalScale, _Smoothness, _Metallic, _OcclusionStrength;
            float _EmissionIntensity, _EmissionSpeed;
            float _Smooth, _SmoothValue, _highSmooth, _TerrainHighEnum, _GroundHeight;
            float _Cutoff;
            float _LightMapIntensity;
            float _Intensity;
            uniform float _CubeScale, _CubeMapBlend;
        CBUFFER_END

        struct v2f
        {
            float4 positionCS  : SV_POSITION;
            float4 color       : COLOR;
            float4 normalWS    : NORMAL;
            float4 tangentWS   : TANGENT;
            float4 bitangentWS : TEXCOORD0;
            float4 TtoW0       : TEXCOORD1;
            float4 TtoW1       : TEXCOORD2;
            float4 TtoW2       : TEXCOORD3;
            float2 uv          : TEXCOORD4;
            float2 uv1         : TEXCOORD5;
            float2 uv2         : TEXCOORD6;
        };

        struct appdata
        {
            float4 positionOS  : POSITION;
            float4 normalOS    : NORMAL;
            float4 tangentOS   : TANGENT;
            float2 texcoord    : TEXCOORD0;
            float2 texcoord1   : TEXCOORD1;
        };
        
        ENDHLSL 
        /*
        Pass
        {
            Name "PlanarShadow"
        	Tags 
	        {
		        "RenderType"="TransparentCutout" 
	        }
            //用使用模板测试以保证alpha显示正确
            Stencil
            {
                Ref 0
                Comp equal
                Pass incrWrap
                Fail keep
                ZFail keep
            }
            Cull Off
            Blend DstColor OneMinusSrcAlpha
            //关闭深度写入
            ZWrite off
            //深度稍微偏移防止阴影与地面穿插
            Offset -1 , 0
            HLSLPROGRAM
            // #pragma shader_feature _ALPHATEST_ON
            // #pragma shader_feature _ALPHAPREMULTIPLY_ON
            #pragma multi_compile_instancing
            #pragma vertex vert
            #pragma fragment frag
            float3 ShadowProjectPos(float4 vertPos)
            {  
                
                float3 shadowPos;
                float3 worldPos =TransformObjectToWorld(vertPos).xyz;
                float3 center = float3(unity_ObjectToWorld[0].w, unity_ObjectToWorld[1].w, unity_ObjectToWorld[2].w);
                float Height=center.y+_GroundHeight;
                Light_Custom light;
                float3 lightDir = GetLightInf().direction;
                float3 lightColor = GetLightInf().color;
                shadowPos.y = min(worldPos .y , Height);
                shadowPos.xz = worldPos .xz - lightDir.xz * max(0 , worldPos .y - Height) / lightDir.y; 
                return shadowPos;
            }
            float GetAlpha (v2f i) {
                float alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex,i.uv).a;
                return alpha;
            }
            v2f vert (appdata v)
            {
        		UNITY_SETUP_INSTANCE_ID(v);
                v2f o;
        		UNITY_TRANSFER_INSTANCE_ID(v,o);
                //得到阴影的世界空间坐标
                float3 shadowPos = ShadowProjectPos(v.positionOS);
                //转换到裁切空间
                o.positionCS = TransformWorldToHClip(shadowPos);
                Light_Custom light;
                float3 lightDir = GetLightInf().direction;
                float3 lightColor = GetLightInf().color;
                float4 ShadowColor = GetLightInf().shadowColor;
                o.color = ShadowColor;
                o.color.a = ShadowColor.a;
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.normalWS.xyz=TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 frag (v2f i) : SV_Target
            {
            	UNITY_SETUP_INSTANCE_ID(i);
                float alpha = GetAlpha(i);
                i.color.a *= step(0.5, alpha);
                // if(_TerrainHighEnum == 1)
                // {
                //     i.color = lerp(i.color, float4(0,0,0,0), clamp((i.posWorld.y / _highSmooth) - 1,0,1));
                // }
                // else
                // {
                //     //i.color = lerp(i.color, float4(0,0,0,0), clamp(1 -((i.posWorld.y+0.1) / _highSmooth),0,1));
                //     clip(i.color-(1-i.posWorld.y / _highSmooth));
                // }
                clip(i.color.a-0.1);
                return i.color;
            }
            ENDHLSL
        }
        */
        Pass
        {
            Tags
            {
                "LightMode" = "UniversalForward"
            }
            Cull [_Cull]
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert 
            #pragma fragment frag
            #pragma multi_compile _ BUILD_SELECT
            #pragma shader_feature use_SelfLight
            #pragma shader_feature use_LightMap
            #pragma shader_feature use_Control
            #pragma shader_feature use_Blend
            #pragma multi_compile _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma target 3.0

            inline float myLerp(float a, float b, float w) 
            {
                return clamp(a + w * (b - a) * _SmoothValue, 0, 1);
            }
            
            v2f vert(appdata v)
            {
                v2f o = (v2f)0;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS.xyz = normalize(TransformObjectToWorldNormal(v.normalOS.xyz));
                o.tangentWS.xyz = normalize(TransformObjectToWorldDir(v.tangentOS.xyz));
                o.bitangentWS.xyz = cross(o.normalWS.xyz, o.tangentWS.xyz) * v.tangentOS.w
                              * unity_WorldTransformParams.w;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.normalWS.w = positionWS.x;
                o.tangentWS.w = positionWS.y;
                o.bitangentWS.w = positionWS.z;
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.uv1 = TRANSFORM_TEX(v.texcoord1, _LightMapTex);
                o.uv2 = v.texcoord1.xy * unity_LightmapST.xy + unity_LightmapST.zw;
                return o;  
            }

            float4 frag(v2f i) : SV_TARGET
            {
                float4 Albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                Albedo.rgb *= _BaseColor.rgb;

                float4 MRA = SAMPLE_TEXTURE2D(_MRA, sampler_MRA, i.uv);
                float AOInf = SAMPLE_TEXTURE2D(_MRA, sampler_MRA, i.uv1).a;
                float AO = lerp(1, AOInf, _OcclusionStrength);
                float Smooth = MRA.g;
                float Metallic = MRA.r * _Metallic;

                float4 normal = SAMPLE_TEXTURE2D(_Normal, sampler_Normal, i.uv);
                float3 normalScale = UnpackNormalScale(normal, _NormalScale);
                normalScale.z = sqrt(1 - saturate(dot(normalScale.xy, normalScale.xy)));
                float3 normalLocal = normalize(normalScale.rgb);
                float3x3 TtoW = {i.tangentWS.xyz, i.bitangentWS.xyz, i.normalWS.xyz};
                TtoW = transpose(TtoW);
                float3 normalDir = NormalizeNormalPerPixel(mul(TtoW, normalScale));
                
                ///Gloss
                float Gloss = (1 - Smooth) * _Smoothness;
                float perceptualRoughness = Smooth;
                perceptualRoughness = lerp(min(perceptualRoughness + 0.2, 1), max(perceptualRoughness - 0.2, 0), _Smoothness);
                float roughness = pow(perceptualRoughness, 2);
                ///Light
                Light_Custom light;
                float3 lightDir = GetLightInf().direction;
                float3 lightColor = GetLightInf().color;
                float3 ambient = CalcAmbientColor(normalDir);
                ///LightMap
                #if defined(use_LightMap)
                    float4 LightMap = SAMPLE_TEXTURE2D(_LightMapTex, sampler_LightMapTex, i.uv1);
                    Albedo.rgb = Albedo.rgb * (LightMap.rgb * _LightMapIntensity + (1 - _LightMapIntensity)) + Albedo.rgb * _LightMapIntensity * LightMap; 
                #endif
                ///Vector
                float3 positionWS = float3(i.normalWS.w, i.tangentWS.w, i.bitangentWS.w);
                float3 viewDir = SafeNormalize(_WorldSpaceCameraPos - positionWS);
                float3 halfVec = normalize(viewDir + lightDir);
                float3 NdotL = max(0.000001, saturate(dot(normalDir, lightDir)));
                float3 NdotH = max(0.000001, saturate(dot(normalDir, halfVec)));
                float3 LdotH = max(0.000001, saturate(dot(lightDir, halfVec)));
                float3 NdotV = max(0.000001, saturate(dot(normalDir, viewDir)));
                float3 VdotH = max(0.000001, saturate(dot(viewDir, halfVec)));
                float3 viewReflectDir = reflect(-viewDir, normalLocal);
                ///CubeMap
                float4 CubeMap = SAMPLE_TEXTURECUBE(_CubeMap, sampler_CubeMap, normalize(viewReflectDir));
                float3 CubeMapBlendColor = Albedo.rgb + CubeMap.rgb * Gloss * _CubeScale;
                Albedo.rgb = lerp(Albedo.rgb, lerp(Albedo.rgb, lerp(Albedo.rgb, CubeMap.rgb * _Smoothness, _Smoothness), Smooth), _CubeMapBlend);
                Albedo.rgb = lerp(Albedo.rgb, CubeMapBlendColor, _CubeMapBlend);
                ///D
                float roughness2 = pow(roughness, 2);
                float NdotH2 = (pow(NdotH, 2) * (roughness2 - 1) + 1);
                float D = roughness2 / (pow(NdotH2, 2) * PI);
                ///G
                float K = pow(roughness + 1, 2) / 8;
                float GLeft = NdotL / lerp(NdotL, 1, K);
                float GRight = NdotV / lerp(NdotV, 1, K);
                float G = GLeft * GRight;
                ///F
                float3 F0 = lerp(0.04, Albedo.rgb, Metallic);
                float3 F = lerp(exp2((-5.55473 * LdotH - 6.98316) * LdotH), 1, F0);
                ///Specular
                float3 SpecularResult = D * F * G / (4 * NdotL * NdotV);
                float3 DirectSpecColor = SpecularResult * lightColor * NdotL * PI;
                DirectSpecColor *= _HightlightColor.rgb;
                ///Diffuse
                float3 kd = (1 - F) * (1 - Metallic);
                float3 DirectDiffuseColor = kd * Albedo.rgb * lightColor;
                float3 DirectColor = DirectSpecColor + DirectDiffuseColor * NdotL;
                ///Indirect Diffuse light
                float3 InDirKS = F0 + exp((-5.55473 * NdotV - 6.98316) * NdotV) * saturate(1 - roughness - F0);
                float3 InDirKD = (1 - InDirKS) * (1 - Metallic);
                //float3 Ambient = 
                float3 InDirDiffuseColor = AO * InDirKD * Albedo.rgb * ambient;
                ///Indirect Specular light
                float3 reflectDirWS = reflect(-viewDir, normalDir);
                float CubeRoughness = perceptualRoughness * (1.7 - 0.7 * perceptualRoughness);
                float MidLevel = CubeRoughness * UNITY_SPECCUBE_LOD_STEPS;
                float4 speColor = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflectDirWS, MidLevel);
                #if !defined(UNITY_USE_NATIVE_HDR)
                    float3 IndirSpeCubeColor = DecodeHDREnvironment(speColor,unity_SpecCube0_HDR) * AO;
                #else
                    float3 IndirSpeCubeColor = speColor.xyz * AO;
                #endif

                float SurReduction = 1 / (pow(roughness, 2) + 1);
                #if defined(SHADER_API_GLES)
                    float Reflectivity = SpecularResult.x;
                #else
                    float Reflectivity = max(max(SpecularResult.x, SpecularResult.y), SpecularResult.z);
                #endif

                float GrazingTSection = saturate(Reflectivity + (1 - Smooth));
                float Fre = Pow4(1 - NdotV);  
                float3 IndirSpeCubeFactor=lerp(F0, GrazingTSection, Fre) * SurReduction;
                float3 IndirSpecColor= IndirSpeCubeColor * IndirSpeCubeFactor * GetLightInf().EnvColor * GetLightInf().EnvExposure;
                float3 IndirColor = IndirSpecColor + InDirDiffuseColor;
                float3 finalColor = DirectColor + IndirColor;

                ///SelfColor
                #if defined(use_SelfLight)
                    float4 Emission = SAMPLE_TEXTURE2D(_Emission, sampler_Emission, i.uv);
                    float Expend = 1;
                    #if defined(use_Control)
                        Expend = abs(_SinTime.w * _EmissionSpeed);
                    #endif
                    float4 EmissionColor = lerp(_EmissionColor, _EmissionColor * 1.2, Expend);
                    finalColor.rgb += Emission.rgb * EmissionColor.rgb * _EmissionIntensity;
                #endif
                #if defined(use_Blend)
                float x = smoothstep(0, _Smooth,  i.uv.x);
                float y = smoothstep(1, 1 - _Smooth, i.uv.x);
                float z = smoothstep(0, _Smooth,i.uv.y);
                float w = smoothstep(1, 1 - _Smooth, i.uv.y);
                Albedo.a = Albedo.a*_MainAlpha;
                if(_TerrainHighEnum == 1)
                {
                    Albedo.a = myLerp(Albedo.a, z * w, clamp((positionWS.y / _highSmooth) - 1, 0, 1));
                }
                else
                {
                   Albedo.a = myLerp(Albedo.a, x * y * z * w, clamp(1 - (positionWS.y / _highSmooth), 0, 1));
                }
                #endif
                #if defined(_ALPHATEST_ON)
                clip(Albedo.a - _Cutoff);
                #endif

                float4 result = float4(finalColor * _Intensity, Albedo.a);
                #if defined(BUILD_SELECT)
                    float expend = sin(_Time.w * 1.6) * 0.25 + 0.25;
                    result = lerp(result, float4(0, 0, 0, 1), expend);
                #endif
                return result;

            }
            ENDHLSL
        }
		/*
		Pass
        {
            Name "DepthOnly"
            Tags{"LightMode" = "DepthOnly"}

            ZWrite On
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            // Required to compile gles 2.0 with standard srp library
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature _ALPHATEST_ON
            #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
		*/
    }
}
