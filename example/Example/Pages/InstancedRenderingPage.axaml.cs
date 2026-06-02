using Aura3D.Avalonia;
using Aura3D.Core.Geometries;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;

namespace Example.Pages;

public partial class InstancedRenderingPage : UserControl
{
    private CameraController _cameraController;

    InstancedMesh? instancedMesh;
    List<float> instanceRotationAngles = new();
    List<float> instanceRotationSpeeds = new();
    List<Vector3> instancePositions = new();
    int currentGridSize = 0;

    List<double> deltaTimes = new();

    public InstancedRenderingPage()
    {
        InitializeComponent();
        _cameraController = new CameraController(aura3Dview);
    }

    private void Aura3DView_SceneInitialized(object? sender, InitializedRoutedEventArgs e)
    {
        var view = sender as Aura3DView;
        if (view == null)
            return;

        view.AutoRequestNextFrameRendering = false;

        view.MainCamera.Position = new Vector3(0, 15, 30);
        view.MainCamera.RotationDegrees = new Vector3(-20, 0, 0);

        var dl = new DirectionalLight();
        dl.RotationDegrees = new Vector3(-30, -15, 0);
        dl.LightColor = Color.White;
        view.AddNode(dl);

        BuildInstancedGrid(view, 10);

        view.RequestNextFrameRendering();
    }

    private void BuildInstancedGrid(Aura3DView view, int gridSize)
    {
        if (instancedMesh != null)
        {
            view.Remove(instancedMesh);
            instancedMesh = null;
        }

        var sourceMesh = new Mesh();
        sourceMesh.Geometry = new BoxGeometry();

        var material = new Material
        {
            BlendMode = BlendMode.Opaque,
            Channels = new List<Channel>
            {
                new Channel()
                {
                    Name = "BaseColor",
                    Texture = Texture.CreateFromColor(Color.White),
                }
            }
        };

        // 自定义顶点着色器：在 base.vert 基础上加 instanceColor
        material.SetShaderSource("LightPass", ShaderType.Vertex, """
            #version 300 es
            precision mediump float;

            #define BONE_NUMBER 150

            #define MAX_DIRECTIONAL_LIGHTS 4
            #define MAX_POINT_LIGHTS 4
            #define MAX_SPOT_LIGHTS 4

            //{{defines}}

            layout(location = 0) in vec3 position;
            layout(location = 1) in vec2 texCoord;
            layout(location = 2) in vec3 normal;
            layout(location = 3) in vec3 tangent;
            layout(location = 4) in vec3 bitangent;
            layout(location = 5) in vec4 boneIndices;
            layout(location = 6) in vec4 boneWeights;


            #ifdef INSTANCED_MESH
            layout(location = 7) in mat4 modelMatrix;
            layout(location = 11) in mat4 normalMatrix;
            #endif

            layout(location = 15) in vec4 instanceColor;


            #ifdef SKINNED_MESH
            uniform mat4 BoneMatrices[BONE_NUMBER];
            #endif

            #ifndef INSTANCED_MESH
            uniform mat4 modelMatrix;
            uniform mat4 normalMatrix;
            #endif

            uniform mat4 viewMatrix;
            uniform mat4 projectionMatrix;

            out vec2 vTexCoord;
            out vec3 vFragPosition;
            out mat3 vTBN;
            out vec4 vInstanceColor;


            void main()
            {
                vTexCoord = texCoord;

                vec3 T = normalize(mat3(normalMatrix) * tangent);
                vec3 B = normalize(mat3(normalMatrix) * bitangent);
                vec3 N = normalize(mat3(normalMatrix) * normal);
                mat3 TBN = mat3(T, B, N);
                vTBN = TBN;

                vInstanceColor = instanceColor;


            #ifdef SKINNED_MESH

                int idx0 = clamp(int(boneIndices.x), 0, BONE_NUMBER - 1);
                int idx1 = clamp(int(boneIndices.y), 0, BONE_NUMBER - 1);
                int idx2 = clamp(int(boneIndices.z), 0, BONE_NUMBER - 1);
                int idx3 = clamp(int(boneIndices.w), 0, BONE_NUMBER - 1);

                float sum = boneWeights.x + boneWeights.y + boneWeights.z + boneWeights.w;
                vec4 w = (sum > 0.0001) ? boneWeights / sum : vec4(1.0, 0.0, 0.0, 0.0);

                mat4 skinMatrix = w.x * BoneMatrices[idx0];
                skinMatrix      += w.y * BoneMatrices[idx1];
                skinMatrix      += w.z * BoneMatrices[idx2];
                skinMatrix      += w.w * BoneMatrices[idx3];

                vec4 worldPosition = modelMatrix * skinMatrix * vec4(position, 1.0);

            #else
                vec4 worldPosition = modelMatrix * vec4(position, 1.0);
            #endif

                vFragPosition = worldPosition.xyz;
                gl_Position = projectionMatrix * viewMatrix * worldPosition;
            }
            """);

        // 自定义片段着色器：在 base.frag 基础上用 instanceColor 乘 baseColor
        material.SetShaderSource("LightPass", ShaderType.Fragment, """
            #version 300 es
            precision mediump float;
            out vec4 outColor;

            //{{defines}}

            #define MAX_DIRECTIONAL_LIGHTS 4
            #define MAX_POINT_LIGHTS 4
            #define MAX_SPOT_LIGHTS 4


            #define DL_SHADOW_ASSIGN(index) if (DirectionalLights[index].castShadow == 1.0) \
                shadows[index] = CalculateShadow(DirectionalLights[index].shadowMapMatrix, DirectionalLightShadowMaps[index]); \
                else \
                shadows[index] = 1.0;

            #define PL_SHADOW_ASSIGN(index) if (PointLights[index].castShadow == 1.0) \
                shadows[index] = CalculatePointLightShadow(PointLights[index].position, PointLights[index].shadowMapMatrices, PointLightShadowMaps[index]); \
                    else \
                shadows[index] = 1.0;

            #define SP_SHADOW_ASSIGN(index) if (SpotLights[index].castShadow == 1.0) \
                shadows[index] = CalculateShadow(SpotLights[index].shadowMapMatrix, SpotLightShadowMaps[index]); \
                else \
                shadows[index] = 1.0;

            #define REPEAT_DL_SHADOW_ASSIGN_1 DL_SHADOW_ASSIGN(0)
            #define REPEAT_DL_SHADOW_ASSIGN_2 REPEAT_DL_SHADOW_ASSIGN_1; DL_SHADOW_ASSIGN(1)
            #define REPEAT_DL_SHADOW_ASSIGN_3 REPEAT_DL_SHADOW_ASSIGN_2; DL_SHADOW_ASSIGN(2)
            #define REPEAT_DL_SHADOW_ASSIGN_4 REPEAT_DL_SHADOW_ASSIGN_3; DL_SHADOW_ASSIGN(3)


            #define REPEAT_PL_SHADOW_ASSIGN_1 PL_SHADOW_ASSIGN(0)
            #define REPEAT_PL_SHADOW_ASSIGN_2 REPEAT_PL_SHADOW_ASSIGN_1; PL_SHADOW_ASSIGN(1)
            #define REPEAT_PL_SHADOW_ASSIGN_3 REPEAT_PL_SHADOW_ASSIGN_2; PL_SHADOW_ASSIGN(2)
            #define REPEAT_PL_SHADOW_ASSIGN_4 REPEAT_PL_SHADOW_ASSIGN_3; PL_SHADOW_ASSIGN(3)


            #define REPEAT_SP_SHADOW_ASSIGN_1 SP_SHADOW_ASSIGN(0)
            #define REPEAT_SP_SHADOW_ASSIGN_2 REPEAT_SP_SHADOW_ASSIGN_1; SP_SHADOW_ASSIGN(1)
            #define REPEAT_SP_SHADOW_ASSIGN_3 REPEAT_SP_SHADOW_ASSIGN_2; SP_SHADOW_ASSIGN(2)
            #define REPEAT_SP_SHADOW_ASSIGN_4 REPEAT_SP_SHADOW_ASSIGN_3; SP_SHADOW_ASSIGN(3)



            struct s_directional_light_info
            {
                vec3 color;
                vec3 direction;
                mat4 shadowMapMatrix;
                float castShadow;
            };

            struct s_point_light_info
            {
                vec3 color;
                vec3 position;
                float radius;
                float softRatio;
                float castShadow;
                mat4 shadowMapMatrices[6];
            };

            struct s_spot_light_info
            {
                vec3 color;
                vec3 position;
                vec3 direction;
                float radius;
                float softRatio;
                float inner_cone_cos;
                float outer_cone_cos;
                float castShadow;
                mat4 shadowMapMatrix;
            };


            in vec2 vTexCoord;
            in vec3 vFragPosition;
            in mat3 vTBN;
            in vec4 vInstanceColor;


            uniform sampler2D BaseColorTexture;
            uniform sampler2D NormalTexture;

            uniform float ambientIntensity;

            uniform vec3 cameraPosition;

            #if defined(BLENDMODE_MASKED) || defined(BLENDMODE_TRANSLUCENT)

            uniform float alphaCutoff;

            #endif

            uniform s_directional_light_info DirectionalLights[MAX_DIRECTIONAL_LIGHTS];
            uniform s_point_light_info PointLights[MAX_POINT_LIGHTS];
            uniform s_spot_light_info SpotLights[MAX_SPOT_LIGHTS];


            uniform sampler2D DirectionalLightShadowMaps[MAX_DIRECTIONAL_LIGHTS];
            uniform samplerCube PointLightShadowMaps[MAX_POINT_LIGHTS];
            uniform sampler2D SpotLightShadowMaps[MAX_SPOT_LIGHTS];

            vec3 CalculateDirectionalLight(vec3 lightDirection, vec3 lightColor, vec3 baseColor, vec3 normal);
            vec3 CalculatePointLight(vec3 lightPosition, vec3 lightColor, float radius,float softRatio, vec3 baseColor, vec3 normal);
            vec3 CalculateSpotLight(vec3 lightPosition, vec3 lightColor, vec3 lightDirection, float radius, float softRatio,float inner_cone_cos, float outer_cone_cos, vec3 baseColor, vec3 normal);
            float CalculateShadow(mat4 shadowMatrix, sampler2D shadowMap);
            float CalculatePointLightShadow(vec3 position, mat4 shadowMapMatrices[6], samplerCube shadowMapTexture);


            float CalcPointLightAttenuation(float d, float r, float softRatio) {
                if (d > r) return 0.0;

                float nd = d / r;
                float atten = 1.0 / (1.0 + 25.0 * nd * nd);
                float softThresh = r * softRatio;
                float soft = smoothstep(r, softThresh, d);
                return atten * soft;
            }

            void main()
            {
                vec4 baseColor = texture(BaseColorTexture, vTexCoord);
                baseColor *= vInstanceColor;


                vec3 normal = texture(NormalTexture, vTexCoord).xyz;
                normal = normalize(normal * 2.0 - 1.0);
                normal = normalize(vTBN * normal);

                if (!gl_FrontFacing)
                {
                    normal = -normal;
                }

                #if defined(BLENDMODE_MASKED) || defined(BLENDMODE_TRANSLUCENT)
                    if (baseColor.a <= alphaCutoff)
                        discard;
                #endif

                vec3 finalColor = vec3(0.0);


                finalColor += baseColor.xyz * ambientIntensity;
                float shadows[10];
                REPEAT_DL_SHADOW_ASSIGN_4//
                for(int i = 0; i < MAX_DIRECTIONAL_LIGHTS; ++i)
                {
                    vec3 color = CalculateDirectionalLight(DirectionalLights[i].direction, DirectionalLights[i].color, baseColor.xyz, normal);

                    if (DirectionalLights[i].castShadow == 0.0)
                        shadows[i] = 1.0;

                    finalColor += (color * shadows[i]);
                }

                REPEAT_PL_SHADOW_ASSIGN_4//
                for(int i = 0; i < MAX_POINT_LIGHTS; ++i)
                {
                    vec3 color = CalculatePointLight(PointLights[i].position, PointLights[i].color,PointLights[i].radius,PointLights[i].softRatio, baseColor.xyz, normal);

                    if (PointLights[i].castShadow == 0.0)
                        shadows[i] = 1.0;

                    finalColor += (color * shadows[i]);
                }

                REPEAT_SP_SHADOW_ASSIGN_4//
                for(int i = 0; i < MAX_SPOT_LIGHTS; ++i)
                {
                    vec3 color = CalculateSpotLight(SpotLights[i].position, SpotLights[i].color, SpotLights[i].direction, SpotLights[i].radius, SpotLights[i].softRatio, SpotLights[i].inner_cone_cos, SpotLights[i].outer_cone_cos, baseColor.xyz, normal);

                    if (SpotLights[i].castShadow == 0.0)
                        shadows[i] = 1.0;

                    finalColor += (color * shadows[i]);
                }

            #ifdef BLENDMODE_TRANSLUCENT

                outColor = vec4(finalColor, baseColor.a);
            #else

                outColor = vec4(finalColor, 1.0);
            #endif

            }


            vec3 CalculateDirectionalLight(vec3 lightDirection, vec3 lightColor, vec3 baseColor, vec3 normal)
            {
                float diff = max(dot(normal, -lightDirection), 0.0);

                vec3 viewDir = normalize(cameraPosition - vFragPosition);

                vec3 halfVector = normalize(viewDir - lightDirection);

                float specular = pow(max(dot(normal, halfVector), 0.0), 32.0);

                float F0 = 0.02;

                return (diff + specular) * lightColor * baseColor;
            }

            vec3 CalculatePointLight(vec3 lightPosition, vec3 lightColor, float radius, float softRatio, vec3 baseColor, vec3 normal)
            {
                vec3 lightDir = normalize(lightPosition - vFragPosition);

                float diff = max(dot(normal, lightDir), 0.0);

                float distance = length(lightPosition - vFragPosition);

                float attenuation = CalcPointLightAttenuation(distance, radius, softRatio);


                vec3 viewDir = normalize(cameraPosition - vFragPosition);

                vec3 halfVector = normalize(viewDir + lightDir);

                float specular = pow(max(dot(normal, halfVector), 0.0), 32.0);

                return (diff + specular) * attenuation * lightColor * baseColor;
            }

            vec3 CalculateSpotLight(vec3 lightPosition, vec3 lightColor, vec3 lightDirection, float radius, float softRatio, float inner_cone_cos, float outer_cone_cos, vec3 baseColor, vec3 normal)
            {
                vec3 lightDir = normalize(lightPosition - vFragPosition);

                float diff = max(dot(normal, lightDir), 0.0);

                float distance = length(lightPosition - vFragPosition);

                float attenuation = CalcPointLightAttenuation(distance, radius, softRatio);


                float theta = dot(lightDir, normalize(-lightDirection));

                float epsilon = (inner_cone_cos - outer_cone_cos);

                float intensity = clamp((theta - outer_cone_cos) / epsilon, 0.0, 1.0);



                vec3 viewDir = normalize(cameraPosition - vFragPosition);

                vec3 halfVector = normalize(viewDir + lightDir);

                float specular = pow(max(dot(normal, halfVector), 0.0), 32.0);


                return (diff + specular) * intensity * attenuation * lightColor * baseColor;
            }

            float CalculateShadow(mat4 shadowMatrix, sampler2D shadowMap)
            {
                vec4 shadowCoord = shadowMatrix * vec4(vFragPosition, 1.0);


                if (shadowCoord.x < -shadowCoord.w || shadowCoord.x > shadowCoord.w ||
                    shadowCoord.y < -shadowCoord.w || shadowCoord.y > shadowCoord.w ||
                    shadowCoord.z < -shadowCoord.w || shadowCoord.z > shadowCoord.w)
                    return 1.0;

                shadowCoord /= shadowCoord.w;

                shadowCoord.xyz = shadowCoord.xyz * 0.5 + 0.5;


                float shadowValue = texture(shadowMap, shadowCoord.xy).x;
                float bias = 0.001;
                if (shadowValue < shadowCoord.z - bias)
                    return 0.0;
                else
                    return 1.0;
            }




            float CalculatePointLightShadow(vec3 position, mat4 shadowMapMatrices[6], samplerCube shadowMapTexture)
            {
                vec3 fragToLight = vFragPosition - position;
                int face = 0;
                float maxComp = 0.0;
                if(abs(fragToLight.x) > maxComp) {
                    face = fragToLight.x > 0.0 ? 0 : 1;
                    maxComp = abs(fragToLight.x);
                }
                if(abs(fragToLight.y) > maxComp) {
                    face = fragToLight.y > 0.0 ? 2 : 3;
                    maxComp = abs(fragToLight.y);
                }
                if(abs(fragToLight.z) > maxComp) {
                    face = fragToLight.z > 0.0 ? 4 : 5;
                    maxComp = abs(fragToLight.z);
                }

                vec4 shadowCoord = shadowMapMatrices[face] * vec4(vFragPosition, 1.0);
                shadowCoord /= shadowCoord.w;
                shadowCoord.xyz = shadowCoord.xyz * 0.5 + 0.5;

                vec3 sampleDir = normalize(fragToLight);
                float shadowValue = texture(shadowMapTexture, sampleDir).r;

                float bias = 0.001;

                if (shadowValue < shadowCoord.z - bias)
                    return 0.0;
                else
                    return 1.0;

            }
            """);

        sourceMesh.Material = material;

        instancedMesh = InstancedMesh.FromMesh(sourceMesh);

        instanceRotationAngles.Clear();
        instanceRotationSpeeds.Clear();
        instancePositions.Clear();

        var rand = new Random(42);
        float spacing = 2.5f;
        float offset = (gridSize - 1) * spacing / 2f;

        var instanceColors = new List<Vector4>();

        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                for (int z = 0; z < gridSize; z++)
                {
                    Vector3 pos = new Vector3(
                        x * spacing - offset,
                        y * spacing - offset,
                        z * spacing - offset);

                    Matrix4x4 transform = Matrix4x4.CreateTranslation(pos);
                    instancedMesh.AddInstance(transform);

                    instancePositions.Add(pos);
                    instanceRotationAngles.Add((float)(rand.NextDouble() * Math.PI * 2));
                    instanceRotationSpeeds.Add((float)(rand.NextDouble() * 2 - 1));
                    instanceColors.Add(new Vector4(
                        (float)rand.NextDouble(),
                        (float)rand.NextDouble(),
                        (float)rand.NextDouble(),
                        1.0f));
                }
            }
        }

        // 逐实例颜色复用 TexCoord_1（location=15），自定义 shader 中声明对应 attribute
        instancedMesh.SetInstanceAttribute<Vector4>(
            BuildInVertexAttribute.TexCoord_1, 4, instanceColors);

        view.AddNode(instancedMesh);
        currentGridSize = gridSize;

        fpsText.Text = $"Instances: {instancedMesh.InstanceCount} | FPS: --";
    }

    private void Aura3DView_SceneUpdated(object? sender, UpdateRoutedEventArgs args)
    {
        var deltaTime = args.DeltaTime;

        if (deltaTimes.Count >= 10)
            deltaTimes.RemoveAt(0);
        deltaTimes.Add(deltaTime);
        var avgDt = deltaTimes.Average();
        fpsText.Text = $"Instances: {currentGridSize * currentGridSize * currentGridSize} | FPS: {(int)(1 / avgDt)}";

        if (instancedMesh != null)
        {
            for (int i = 0; i < instancedMesh.InstanceCount; i++)
            {
                instanceRotationAngles[i] += instanceRotationSpeeds[i] * (float)deltaTime;

                Matrix4x4 rotation = Matrix4x4.CreateFromYawPitchRoll(
                    instanceRotationAngles[i],
                    instanceRotationAngles[i] * 0.7f,
                    instanceRotationAngles[i] * 0.3f);

                Matrix4x4 transform = rotation * Matrix4x4.CreateTranslation(instancePositions[i]);
                instancedMesh.UpdateInstance(i, transform);
            }
        }

        (sender as Aura3DView)?.RequestNextFrameRendering();
    }

    private void BuildButton_Click(object? sender, RoutedEventArgs e)
    {
        if (int.TryParse(gridSizeTextBox.Text?.Trim(), out int size) && size > 0)
        {
            if (size <= 0) size = 10;
            BuildInstancedGrid(aura3Dview, size);
        }
    }
}
