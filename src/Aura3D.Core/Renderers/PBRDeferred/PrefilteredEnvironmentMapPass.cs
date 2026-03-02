using Aura3D.Core.Nodes;
using Silk.NET.OpenGLES;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Aura3D.Core.Renderers.PBRDeferred;

internal class PrefilteredEnvironmentMapPass : RenderPass
{
    const int PREFILTER_WIDTH = 256;

    const int MAX_MIP_LEVELS = 8;
    public PrefilteredEnvironmentMapPass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
        VertexShader = @"#version 300 es
        precision highp float;
        layout (location = 0) in vec3 aPos;
        out vec3 localPos;
        uniform mat4 projection;
        uniform mat4 view;
        void main() {
            localPos = aPos;
            gl_Position = projection * view * vec4(localPos, 1.0);
        }
";
        FragmentShader = @"#version 300 es
        precision highp float;
        in vec3 localPos;
        out vec4 fragColor;
        uniform samplerCube environmentMap;
        uniform float roughness;
        const float PI = 3.14159265359;
        const uint SAMPLE_COUNT = 1024u;

        float RadicalInverse_VdC(uint bits) {
            bits = (bits << 16u) | (bits >> 16u);
            bits = ((bits & 0x55555555u) << 1u) | ((bits & 0xAAAAAAAAu) >> 1u);
            bits = ((bits & 0x33333333u) << 2u) | ((bits & 0xCCCCCCCCu) >> 2u);
            bits = ((bits & 0x0F0F0F0Fu) << 4u) | ((bits & 0xF0F0F0F0u) >> 4u);
            bits = ((bits & 0x00FF00FFu) << 8u) | ((bits & 0xFF00FF00u) >> 8u);
            return float(bits) * 2.3283064365386963e-10; // 1/0x100000000
        }

        vec2 Hammersley(uint i, uint N) {
            return vec2(float(i)/float(N), RadicalInverse_VdC(i));
        }

        vec3 ImportanceSampleGGX(vec2 Xi, vec3 N, float roughness) {
            float a = roughness * roughness;
            float phi = 2.0 * PI * Xi.x;
            float cosTheta = sqrt((1.0 - Xi.y) / (1.0 + (a*a - 1.0) * Xi.y));
            float sinTheta = sqrt(1.0 - cosTheta*cosTheta);
            vec3 H = vec3(cos(phi) * sinTheta, sin(phi) * sinTheta, cosTheta);
            vec3 up = abs(N.z) < 0.999 ? vec3(0.0, 0.0, 1.0) : vec3(1.0, 0.0, 0.0);
            vec3 tangent = normalize(cross(up, N));
            vec3 bitangent = cross(N, tangent);
            return normalize(tangent * H.x + bitangent * H.y + N * H.z);
        }

        void main() {
            vec3 N = normalize(localPos);
            vec3 R = N;
            vec3 prefilteredColor = vec3(0.0);
            float totalWeight = 0.0;

            for(uint i = 0u; i < SAMPLE_COUNT; i++) {
                vec2 Xi = Hammersley(i, SAMPLE_COUNT);
                vec3 H = ImportanceSampleGGX(Xi, N, roughness);
                vec3 L = normalize(2.0 * dot(R, H) * H - R);
                float NdotL = max(dot(N, L), 0.0);
                if(NdotL > 0.0) {
                    prefilteredColor += texture(environmentMap, L).rgb * NdotL;
                    totalWeight += NdotL;
                }
            }
            prefilteredColor = prefilteredColor / totalWeight;
            fragColor = vec4(prefilteredColor, 1.0);
        }
";
    }

    public override void BeforeRender(Camera camera)
    {
        base.BeforeRender(camera);
        gl.Disable(EnableCap.DepthTest);
    }

    public override void Render(Camera camera)
    {
        var perfilteredEnvMap = camera.GetPipelineGpuResource<CubeRenderTarget>("PrefilteredEnvironmentMap");
        if (perfilteredEnvMap != null)
            return;
        else
        {
            perfilteredEnvMap = new CubeRenderTarget();

            perfilteredEnvMap.AddRenderTexture("perfilteredEnv", TextureFormat.Rgb16f);

            perfilteredEnvMap.SetMipMapLevel(MAX_MIP_LEVELS);

            perfilteredEnvMap.SetSize(PREFILTER_WIDTH, PREFILTER_WIDTH);

            perfilteredEnvMap.Upload(gl);

            perfilteredEnvMap.SetDepthTexture(TextureFormat.DepthComponent16);

            camera.SetPipelineGpuResource("PrefilteredEnvironmentMap", perfilteredEnvMap);

        }

        UseShader();
        UseShader_Internal(null);

        var projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 2, 1, 0.1f, 10f);

        Span<Matrix4x4> views =
        [
            Matrix4x4.CreateLookAt(Vector3.Zero, Vector3.UnitX, -Vector3.UnitY),
            Matrix4x4.CreateLookAt(Vector3.Zero, -Vector3.UnitX, -Vector3.UnitY),
            Matrix4x4.CreateLookAt(Vector3.Zero, Vector3.UnitY, Vector3.UnitZ),
            Matrix4x4.CreateLookAt(Vector3.Zero, -Vector3.UnitY, -Vector3.UnitZ),
            Matrix4x4.CreateLookAt(Vector3.Zero, Vector3.UnitZ, -Vector3.UnitY),
            Matrix4x4.CreateLookAt(Vector3.Zero, -Vector3.UnitZ, -Vector3.UnitY)
        ];


        var perfilteredEnvMapTexture = perfilteredEnvMap.GetTexture(0)!;



        UniformInt("environmentMap", 0);

        gl.ActiveTexture(TextureUnit.Texture0);

        gl.BindTexture(TextureTarget.TextureCubeMap, camera.SkyboxTexture!.TextureId);

        UniformMatrix4("projection", projection);

        for (int mip = 0; mip < perfilteredEnvMap.MipmapLevel; mip++)
        {
            var roughness = mip / (float)(MAX_MIP_LEVELS - 1);

            var mipWidth = PREFILTER_WIDTH / (1 << mip);

            var mipHeight = PREFILTER_WIDTH / (1 << mip);

            gl.Viewport(0, 0, (uint)mipWidth, (uint)mipHeight);

            UniformFloat("roughness", roughness);

            for (var face = 0; face < 6; face++)
            {
                gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.TextureCubeMapPositiveX + face, perfilteredEnvMapTexture.TextureId, mip);

                // gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.TextureCubeMapPositiveX + face, perfilteredEnvMap.DepthStencilTexture.TextureId, mip);

                UniformMatrix4("view", views[face]);

                RenderCube();

            }
        }



    }
    public override void AfterRender(Camera camera)
    {
        base.AfterRender(camera);
        gl.Enable(EnableCap.DepthTest);
    }

}
