using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;
using System.Numerics;
using Aura3D.Core.Renderers;

namespace Aura3D.Core;

public class PointCloudPass : RenderPass
{
    public float DefaultPointSize { get; set; } = 5.0f;

    public PointCloudPass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
        ShaderName = nameof(PointCloudPass);

        VertexShader = """
            #version 300 es
            precision mediump float;

            //{{defines}}

            layout(location = 0) in vec3 position;
            layout(location = 2) in vec4 color;

            uniform mat4 modelMatrix;
            uniform mat4 viewMatrix;
            uniform mat4 projectionMatrix;
            uniform float uPointSize;

            out vec4 vColor;

            void main()
            {
                vec4 worldPosition = modelMatrix * vec4(position, 1.0);
                gl_Position = projectionMatrix * viewMatrix * worldPosition;
                gl_PointSize = uPointSize;
                vColor = color;
            }
            """;

        FragmentShader = """
            #version 300 es
            precision mediump float;

            //{{defines}}

            in vec4 vColor;
            out vec4 outColor;

            void main()
            {
                float dist = length(gl_PointCoord - 0.5);
                if (dist > 0.5) discard;

                outColor = vColor;
            }
            """;
    }

    public override void BeforeRender(Camera camera)
    {
        BindOutPutRenderTarget(camera);

        gl.Disable(EnableCap.Blend);
        gl.Enable(EnableCap.DepthTest);
        gl.DepthMask(true);
        gl.DepthFunc(DepthFunction.Less);
        gl.Disable(EnableCap.CullFace);
    }

    public override void Render(Camera camera)
    {
        UseShader();
        RenderVisibleMeshesInCamera(
            mesh => IsPointCloudMesh(mesh) && IsMaterialBlendMode(mesh, BlendMode.Opaque),
            camera.View, camera.Projection);

        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);
        gl.DepthMask(false);

        UseShader("BLENDMODE_TRANSLUCENT");
        RenderVisibleMeshesInCamera(
            mesh => IsPointCloudMesh(mesh) && IsMaterialBlendMode(mesh, BlendMode.Translucent),
            camera.View, camera.Projection);

        gl.DepthMask(true);
        gl.Disable(EnableCap.Blend);
    }

    public override void AfterRender(Camera camera)
    {
    }

    public override void RenderMesh(Mesh mesh, Matrix4x4 view, Matrix4x4 projection)
    {
        ClearTextureUnit();

        UniformMatrix4("viewMatrix", view);
        UniformMatrix4("projectionMatrix", projection);

        float pointSize = DefaultPointSize;
        if (mesh.Material != null &&
            mesh.Material.TryGetParameterValue<float>("uPointSize", out var materialPointSize))
        {
            pointSize = materialPointSize;
        }
        UniformFloat("uPointSize", pointSize);

        base.RenderMesh(mesh, view, projection);
    }

    private static bool IsPointCloudMesh(Mesh mesh)
    {
        return mesh.Geometry != null
            && mesh.Geometry.PrimitiveType == Resources.PrimitiveType.Points;
    }
}
