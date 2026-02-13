using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Silk.NET.OpenGLES;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aura3D.Core.Renderers.PBRDeferred;

internal class ConstantAmbientPass : RenderPass
{
    string GbufferRenderTargetName;
    public ConstantAmbientPass(RenderPipeline renderPipeline, string gbufferRendertarget) : base(renderPipeline)
    {
        GbufferRenderTargetName = gbufferRendertarget;

        this.VertexShader = ShaderResource.pbr_directionallight_lighting_pass_vert;

        FragmentShader = @"#version 300 es
precision highp float;
//{{defines}}
layout (location = 0) out vec4 FragColor;

#ifdef ENBALE_DEFERRED_SHADING

in vec2 TexCoords;
uniform sampler2D gBufferBaseColor;
uniform sampler2D gBufferNormalRoughness;
uniform sampler2D gBufferMetallicEmissive;
uniform sampler2D depthTexture;

uniform mat4 invProjection;
uniform mat4 invView;

#else

uniform sampler2D Texture_BaseColor;
uniform sampler2D Texture_Normal;
uniform sampler2D Texture_MetallicRoughness;
uniform sampler2D Texture_Emissive;
uniform sampler2D Texture_Occlusion;


in vec2 vTexCoord;
in vec3 vFragPosition;
in mat3 vTBN;
#endif

uniform vec3 ambientColor;
uniform float ambientIntensity;

uniform vec3 viewPos;

vec3 calcConstantAmbientPBR(vec3 N, vec3 V, vec3 albedo, float metalness, float roughness, vec3 ambientColor, float ambientIntensity) 
{
    vec3 F0 = mix(vec3(0.04), albedo, metalness);
    
    float NdotV = max(dot(N, V), 0.001);
    vec3 F = F0 + (max(vec3(1.0 - roughness), F0) - F0) * pow(clamp(1.0 - NdotV, 0.0, 1.0), 5.0);
    
    vec3 kS = F;
    vec3 kD = (1.0 - kS) * (1.0 - metalness);
    
    vec3 ambientDiffuse = kD * albedo * ambientColor * ambientIntensity;
    
    float specularFactor = pow(1.0 - roughness, 2.0); 
    vec3 ambientSpecular = F * ambientColor * ambientIntensity * specularFactor;
    
    return ambientDiffuse + ambientSpecular;
}

#ifdef ENBALE_DEFERRED_SHADING
vec3 reconstructWorldPosFromDepth(vec2 texCoords) {
    float depth = texture(depthTexture, texCoords).r;
    vec3 ndc;
    ndc.xy = texCoords * 2.0 - 1.0;
    ndc.z = depth * 2.0 - 1.0;
    vec4 clipPos = vec4(ndc, 1.0);
    vec4 viewPos = invProjection * clipPos;
    viewPos /= viewPos.w;
    vec4 worldPos = invView * viewPos;
    return worldPos.xyz;
}
#endif


void main()
{
#ifdef ENBALE_DEFERRED_SHADING
    vec4 baseColor = texture(gBufferBaseColor, TexCoords);
    vec4 metallicEmissive = texture(gBufferMetallicEmissive, TexCoords);
    vec3 albedo = baseColor.xyz;
    float metalness = metallicEmissive.x;

    vec4 normalRough = texture(gBufferNormalRoughness, TexCoords);
    vec3 N = normalize(normalRough.rgb * 2.0 - 1.0);
    float roughness = clamp(normalRough.a, 0.01, 0.99);

    vec3 fragPosWorld = reconstructWorldPosFromDepth(TexCoords);

#else
    vec4 baseColor = texture(Texture_BaseColor, vTexCoord);
    vec3 normal = texture(Texture_Normal, vTexCoord).xyz;
    vec4 metalness_roughness = texture(Texture_MetallicRoughness, vTexCoord);
    
    normal = normalize(normal.xyz * 2.0 - 1.0);
    normal = normalize(vTBN * normal);
    
	if (!gl_FrontFacing) 
	{
		normal = -normal;
	}
    vec3 N = normal;
    vec3 albedo = baseColor.rgb;
    float metalness = metalness_roughness.x;
    float roughness = metalness_roughness.y;

    vec3 fragPosWorld = vFragPosition;

#endif

    vec3 V = normalize(viewPos - fragPosWorld);
    vec3 lightContribution = calcConstantAmbientPBR(N, V, albedo, metalness, roughness, ambientColor, ambientIntensity);


#ifdef ENBALE_DEFERRED_SHADING
    float alpha = baseColor.a;
#endif

#ifdef BLENDMODE_TRANSLUCENT
#ifdef IS_FIRST_LIGHT
    float alpha = baseColor.a;
#else
    float alpha = 0.0;
#endif
    
#endif
    FragColor = vec4(lightContribution, alpha);
}


";
    }

    public override void BeforeRender(Camera camera)
    {
        gl.Disable(EnableCap.DepthTest);

        gl.Enable(EnableCap.Blend);

        gl.BlendFunc(BlendingFactor.One, BlendingFactor.One);

        gl.BlendEquation(BlendEquationModeEXT.FuncAdd);

    }

    public override void Render(Camera camera)
    {
        BindOutPutRenderTarget(camera);
        gl.ClearColor(0, 0, 0, 0);
        gl.Clear(ClearBufferMask.ColorBufferBit);

        var size = new Size((int)camera.RenderTarget.Width, (int)camera.RenderTarget.Height);
        var rt = GetRenderTarget(GbufferRenderTargetName, size);

        var gBufferBaseColor = rt.GetTexture("BaseColor");
        var gBufferNormalRoughness = rt.GetTexture("NormalRoughness");
        var gBufferMetallicEmissive = rt.GetTexture("MetallicEmissive");
        var depthTexture = rt.DepthStencilTexture;

        UseShader("ENBALE_DEFERRED_SHADING");
        UseShader_Internal(null);
        ClearTextureUnit();

        UniformTexture(nameof(gBufferBaseColor), gBufferBaseColor);
        UniformTexture(nameof(gBufferNormalRoughness), gBufferNormalRoughness);
        UniformTexture(nameof(gBufferMetallicEmissive), gBufferMetallicEmissive);
        UniformTexture(nameof(depthTexture), depthTexture);
        UniformVector3("viewPos", camera.WorldTransform.Translation);
        UniformMatrix4("invProjection", camera.Projection.Inverse());
        UniformMatrix4("invView", camera.View.Inverse());
        UniformColor("ambientColor", Color.White);
        UniformFloat("ambientIntensity", 0.05f);

        RenderQuad();
    }
}
