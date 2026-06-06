using Aura3D.Core.Nodes;
using Aura3D.Core.Particles;
using Aura3D.Core.Renderers;
using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;
using System.Numerics;

namespace Aura3D.Core;

/// <summary>
/// Particle render pass using billboard quads + GPU instancing.
/// Register this pass in a pipeline's EveryCameraRenderPasses to enable particle rendering.
/// </summary>
public class ParticlePass : RenderPass
{
    public float DefaultParticleSize { get; set; } = 1.0f;
    public float GlobalAlpha { get; set; } = 1.0f;

    public ParticlePass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
        ShaderName = nameof(ParticlePass);

        VertexShader = """
            #version 300 es
            precision mediump float;

            //{{defines}}

            layout(location = 0) in vec3 position;
            layout(location = 1) in vec2 texCoord;
            layout(location = 8) in mat4 instanceTransform;
            layout(location = 2) in vec4 instanceColor;
            layout(location = 3) in float instanceSize;

            uniform mat4 viewMatrix;
            uniform mat4 projectionMatrix;
            uniform vec3 cameraRight;
            uniform vec3 cameraUp;
            uniform float uGlobalAlpha;

            out vec2 vTexCoord;
            out vec4 vColor;

            void main()
            {
                vec3 worldPos = vec3(instanceTransform[3][0], instanceTransform[3][1], instanceTransform[3][2]);

                float cosA = instanceTransform[0][0];
                float sinA = instanceTransform[1][0];

                vec3 right = cameraRight * cosA + cameraUp * sinA;
                vec3 upVal = -cameraRight * sinA + cameraUp * cosA;

                vec3 offset = right * position.x + upVal * position.y;
                vec3 billboardPos = worldPos + offset * instanceSize;

                gl_Position = projectionMatrix * viewMatrix * vec4(billboardPos, 1.0);

                vTexCoord = texCoord;
                vColor = instanceColor;
                vColor.a *= uGlobalAlpha;
            }
            """;

        FragmentShader = """
            #version 300 es
            precision mediump float;

            //{{defines}}

            in vec2 vTexCoord;
            in vec4 vColor;
            out vec4 outColor;

            #ifdef PARTICLE_TEXTURE
            uniform sampler2D uParticleTexture;
            #endif

            void main()
            {
                #ifdef PARTICLE_TEXTURE
                vec4 texColor = texture(uParticleTexture, vTexCoord);
                outColor = texColor * vColor;
                #else
                float dist = length(vTexCoord - 0.5) * 2.0;
                float alpha = 1.0 - smoothstep(0.5, 1.0, dist);
                outColor = vec4(vColor.rgb, vColor.a * alpha);
                #endif
            }
            """;
    }

    public override void BeforeRender(Camera camera)
    {
        BindOutPutRenderTarget(camera);
        gl.Enable(EnableCap.DepthTest);
        gl.DepthFunc(DepthFunction.Less);
        gl.Disable(EnableCap.CullFace);
        gl.Disable(EnableCap.Blend);
        gl.DepthMask(true);
    }

    public override void Render(Camera camera)
    {
        Matrix4x4.Invert(camera.View, out var invView);
        var camRight = new Vector3(invView.M11, invView.M12, invView.M13);
        var camUp = new Vector3(invView.M21, invView.M22, invView.M23);

        // Opaque
        UseShader("PARTICLE_OPAQUE");
        RenderParticleGroups(im => IsMaterialBlendMode(im.Material, BlendMode.Opaque),
            camera.View, camera.Projection, camRight, camUp);

        // Masked
        UseShader("PARTICLE_MASKED");
        RenderParticleGroups(im => IsMaterialBlendMode(im.Material, BlendMode.Masked),
            camera.View, camera.Projection, camRight, camUp);

        // Translucent
        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);
        gl.DepthMask(false);

        UseShader("PARTICLE_TRANSLUCENT");
        RenderParticleGroups(im => IsParticleMesh(im) && IsMaterialBlendMode(im.Material, BlendMode.Translucent),
            camera.View, camera.Projection, camRight, camUp);

        gl.DepthMask(true);
        gl.Disable(EnableCap.Blend);
    }

    public override void AfterRender(Camera camera) { }

    private void RenderParticleGroups(
        Func<InstancedMesh, bool> filter, Matrix4x4 view, Matrix4x4 proj,
        Vector3 camRight, Vector3 camUp)
    {
        foreach (var im in renderPipeline.InstancedMeshes)
        {
            if (!im.Enable || im.InstanceCount == 0) continue;
            if (!IsParticleMesh(im)) continue;
            if (!filter(im)) continue;

            UseShader_Internal(im.Material);
            RenderParticleGroup(im, view, proj, camRight, camUp);
        }
    }

    private static bool IsParticleMesh(InstancedMesh im)
    {
        return im.InstanceAttributes.ContainsKey(
            ((BuildInVertexAttribute)ParticleRenderData.InstanceSizeLocation).ToString());
    }

    public unsafe void RenderParticleGroup(
        InstancedMesh im, Matrix4x4 view, Matrix4x4 proj,
        Vector3 camRight, Vector3 camUp)
    {
        ClearTextureUnit();
        UniformMatrix4("viewMatrix", view);
        UniformMatrix4("projectionMatrix", proj);
        UniformVector3("cameraRight", camRight);
        UniformVector3("cameraUp", camUp);
        UniformFloat("uGlobalAlpha", GlobalAlpha);

        if (im.Material != null && im.Material.TryGetParameterValue<ITexture>("uParticleTexture", out var tex))
            UniformTexture("uParticleTexture", tex);

        if (im.Material != null && im.Material.HasShader)
        {
            var cb = im.Material.GetShaderPassParametersCallback(ShaderName);
            cb?.Invoke(this);
        }

        gl.BindVertexArray(im.Vao);

        if (im.IndicesCount > 0)
            gl.DrawElementsInstanced(GLEnum.Triangles, (uint)im.IndicesCount,
                GLEnum.UnsignedInt, (void*)0, (uint)im.InstanceCount);
        else
            gl.DrawArraysInstanced(GLEnum.Triangles, 0, (uint)im.VertexCount, (uint)im.InstanceCount);
    }
}
