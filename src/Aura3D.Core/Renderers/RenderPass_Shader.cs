using Silk.NET.OpenGLES;
using System.Drawing;
using System.Numerics;
using System.Text;
using Aura3D.Core.Math;
using Aura3D.Core.Resources;
using Aura3D.Core.Nodes;
using ShaderType = Silk.NET.OpenGLES.ShaderType;

namespace Aura3D.Core.Renderers;

public partial class RenderPass
{
    public string ShaderName { get; protected set; }

    protected string VertexShader = string.Empty;

    protected string FragmentShader = string.Empty;
    public Dictionary<string, Shader> Shaders { get; } = new Dictionary<string, Shader>();

    public Shader? CurrentShader { get; private set; } = null;

    List<string> defines = [];

    public void UseShader(params string[] defines)
    {
        this.defines = new(defines);
    }

    public void AddDefines(params string[] defines)
    {
        this.defines.AddRange(defines);

    }

    protected void UseShader_Internal(Mesh? mesh)
    {
        Shader? shader = null;

        var name = string.Join(";", defines);

        if (mesh != null && mesh.Material != null && mesh.Material.HasShader)
        {
            var (vertexShader, fragmentShader) = mesh.Material.GetShaderSource(ShaderName);

            if (vertexShader != null || fragmentShader != null)
            {
                if (mesh.Material.Shaders.TryGetValue(name, out shader) == false)
                {
                    if (vertexShader == null)
                        vertexShader = VertexShader;
                    if (fragmentShader == null)
                        fragmentShader = FragmentShader;

                    shader = CreateShaderProgram(defines.ToArray(), vertexShader, fragmentShader);

                    mesh.Material.Shaders[name] = shader;
                }
            }
        }

        if (shader == null)
        {
            if (Shaders.TryGetValue(name, out shader) == false)
            {
                shader = CreateShaderProgram(defines.ToArray(), VertexShader, FragmentShader);
                Shaders[name] = shader;
            }
        }

        gl.UseProgram(shader.ProgramId);
        CurrentShader = shader;
    }

    private Shader CreateShaderProgram(string[] defines, string vertexShader, string fragmentShader)
    {
        var shader = new Shader();

        shader.Defines = defines;
        
        var definesText = string.Join("\n", defines.Select(d => $"#define {d}"));

        var vs = vertexShader.Replace("//{{defines}}", definesText);

        var fs = fragmentShader.Replace("//{{defines}}", definesText);

        var vertex = gl.CreateShader(ShaderType.VertexShader);

        if (System.OperatingSystem.IsMacOS())
        {
            vs = vs.Replace("#version 300 es", "#version 330 core");
            fs = fs.Replace("#version 300 es", "#version 330 core");
        }

        gl.ShaderSource(vertex, vs);

        gl.CompileShader(vertex);


        gl.GetShader(vertex, GLEnum.CompileStatus, out int code);

        if (code == 0)
        {
            var info = gl.GetShaderInfoLog(vertex);
            Console.WriteLine(vs);
            throw new InvalidOperationException($"Vertex shader compilation failed: {info}");
        }

        var fragment = gl.CreateShader(ShaderType.FragmentShader);

        gl.ShaderSource(fragment, fs);

        gl.CompileShader(fragment);

        gl.GetShader(fragment, GLEnum.CompileStatus, out code);

        if (code == 0)
        {
            var info = gl.GetShaderInfoLog(fragment);
            Console.WriteLine(fs);
            throw new InvalidOperationException($"Fragment shader compilation failed: {info}");
        }

        var programId = gl.CreateProgram();

        gl.AttachShader(programId, vertex);

        gl.AttachShader(programId, fragment);

        gl.LinkProgram(programId);

        gl.GetProgram(programId, GLEnum.LinkStatus, out int linkStatus);
        if (linkStatus == 0)
        {
            var info = gl.GetProgramInfoLog(programId);
            throw new InvalidOperationException($"Shader program link failed: {info}");
        }

        gl.DeleteShader(vertex);

        gl.DeleteShader(fragment);

        shader.ProgramId = programId;


        GetAllUniformLocations(gl, shader);

        return shader;
    }
    private unsafe void GetAllUniformLocations(GL gl, Shader shader)
    {
        gl.GetProgram(shader.ProgramId, GLEnum.ActiveUniforms, out int numUniforms);

        if (numUniforms <= 0)
            return;

        gl.GetProgram(shader.ProgramId, GLEnum.ActiveUniformMaxLength, out int maxNameLength);

        Span<byte> nameBuffer = stackalloc byte[maxNameLength];

        for (int i = 0; i < numUniforms; i++)
        {
            gl.GetActiveUniform(shader.ProgramId, (uint)i, out var length, out var size, out GLEnum uniformType, nameBuffer);

            string uniformName = Encoding.UTF8.GetString(nameBuffer.Slice(0, (int)length));

            int location = gl.GetUniformLocation(shader.ProgramId, uniformName);

            shader.UniformLocation[uniformName.Trim()] = location;
        }

    }


    private int currentTextureUnit = 0;
    public void ClearTextureUnit()
    {
        currentTextureUnit = 0;
    }

    public void UniformTexture(string name, uint textureId)
    {
        if (CurrentShader == null)
            return;
        var location = CurrentShader.GetUniformLocation(name, gl);
        if (location == -1)
            return;
        var textureUnit = GLEnum.Texture0 + currentTextureUnit;
        gl.Uniform1(location, currentTextureUnit);
        gl.ActiveTexture(textureUnit);
        gl.BindTexture(GLEnum.Texture2D, textureId);

        currentTextureUnit++;
    }

    public void UniformTextureCubeMap(string name, uint textureId)
    {
        if (CurrentShader == null)
            return;
        var location = CurrentShader.GetUniformLocation(name, gl);
        if (location == -1)
            return;
        var textureUnit = GLEnum.Texture0 + currentTextureUnit;
        gl.Uniform1(location, currentTextureUnit);
        gl.ActiveTexture(textureUnit);
        gl.BindTexture(GLEnum.TextureCubeMap, textureId);
        currentTextureUnit++;
    }

    public void UniformTextureCubeMap(string name, ICubeTexture texture)
    {
        if (CurrentShader == null)
            return;
        var location = CurrentShader.GetUniformLocation(name, gl);
        if (location == -1)
            return;
        var textureUnit = GLEnum.Texture0 + currentTextureUnit;
        gl.Uniform1(location, currentTextureUnit);
        gl.ActiveTexture(textureUnit);
        gl.BindTexture(GLEnum.TextureCubeMap, texture.TextureId);
        currentTextureUnit++;
    }

    public void UniformTexture(string name, ITexture texture)
    {
        if (texture == null)
            return;
        if (CurrentShader == null)
            return;
        var location = CurrentShader.GetUniformLocation(name, gl);
        if (location == -1)
            return;
        var textureUnit = GLEnum.Texture0 + currentTextureUnit;
        gl.ActiveTexture(textureUnit);
        gl.BindTexture(GLEnum.Texture2D, texture.TextureId);
        gl.Uniform1(location, currentTextureUnit);

        currentTextureUnit++;
    }

    public void UniformInt(string name, int value)
    {
        if (CurrentShader == null)
            return;
        var location = CurrentShader.GetUniformLocation(name, gl);
        if (location == -1)
            return;
        gl.Uniform1(location, value);
    }

    public void UniformFloat(string name, float value)
    {
        if (CurrentShader == null)
            return;
        var location = CurrentShader.GetUniformLocation(name, gl);
        if (location == -1)
            return;
        gl.Uniform1(location, value);
    }

    public unsafe void UniformVector3(string name, Vector3 value)
    {
        if (CurrentShader == null)
            return;
        var location = CurrentShader.GetUniformLocation(name, gl);
        if (location == -1)
            return;
        gl.Uniform3(location, 1, (float*)&value);
    }

    public unsafe void UniformMatrix4(string name, Matrix4x4 value)
    {
        if (CurrentShader == null)
            return;
        var location = CurrentShader.GetUniformLocation(name, gl);
        if (location == -1)
            return;
        gl.UniformMatrix4(location, 1, false, (float*)&value);
    }

    public unsafe void UniformVector2(string name, Vector2 value)
    {
        if (CurrentShader == null)
            return;
        var location = CurrentShader.GetUniformLocation(name, gl);
        if (location == -1)
            return;
        gl.Uniform2(location, 1, (float*)&value);
    }

    public unsafe void UniformVector4(string name, Vector4 value)
    {
        if (CurrentShader == null)
            return;
        var location = CurrentShader.GetUniformLocation(name, gl);
        if (location == -1)
            return;
        gl.Uniform4(location, 1, (float*)&value);
    }

    public unsafe void UniformColor(string name, Color color)
    {
        Vector4 vector4 = color.ToVector4();
        UniformVector3(name, new Vector3(vector4.X, vector4.Y, vector4.Z));
    }

    public unsafe void UniformMatrix4Array(string name, Span<Matrix4x4> values)
    {
        if (CurrentShader == null || values == null || values.Length == 0)
            return;
        var location = CurrentShader.GetUniformLocation(name, gl);
        if (location == -1)
            return;
        fixed (Matrix4x4* ptr = values)
        {
            gl.UniformMatrix4(location, (uint)values.Length, false, (float*)ptr);
        }
    }

    public unsafe void UniformVector3Array(string name, Span<Vector3> values)
    {
        if (CurrentShader == null || values == null || values.Length == 0)
            return;
        var location = CurrentShader.GetUniformLocation(name, gl);
        if (location == -1)
            return;
        fixed (Vector3* ptr = values)
        {
            gl.Uniform3(location, (uint)values.Length, (float*)ptr);
        }
    }

    public unsafe void UniformVector4Array(string name, Span<Vector4> values)
    {
        if (CurrentShader == null || values == null || values.Length == 0)
            return;
        var location = CurrentShader.GetUniformLocation(name, gl);
        if (location == -1)
            return;
        fixed (Vector4* ptr = values)
        {
            gl.Uniform4(location, (uint)values.Length, (float*)ptr);
        }
    }
}

public class  Shader
{
    public string[] Defines { get; set; } = [];

    public uint ProgramId { get; set; } = 0;


    public Dictionary<string, int> UniformLocation = new Dictionary<string, int>();

    public int GetUniformLocation(string name, GL gl)
    {
        if (UniformLocation.TryGetValue(name, out int location))
        {
            return location;
        }

        location = gl.GetUniformLocation(ProgramId, name);

        if (location >= 0)
        {
            UniformLocation[name] = location;
            return location;
        }

        return -1;
    }

}