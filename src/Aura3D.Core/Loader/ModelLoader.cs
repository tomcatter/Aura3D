using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using SharpGLTF.Schema2;
using System.Numerics;
using Material = Aura3D.Core.Resources.Material;
using Mesh = Aura3D.Core.Nodes.Mesh;
using Node = Aura3D.Core.Nodes.Node;
using Texture = Aura3D.Core.Resources.Texture;
using TextureWrapMode = Aura3D.Core.Resources.TextureWrapMode;
using SharpGLTF.IO;


namespace Aura3D.Core;

public static class ModelLoader
{
    static HashSet<Func<ModelRoot, SharpGLTF.Schema2.Material, List<Channel>>> _materialExtensions = []; 

    
    public static void RegisterMaterialExtensions<TExtension>(string extensionName, Func<ModelRoot, SharpGLTF.Schema2.Material, List<Channel>> handleFunc) 
        where TExtension : JsonSerializable, new()
    {
        SharpGLTF.Schema2.ExtensionsFactory.RegisterExtension<SharpGLTF.Schema2.Material, TExtension>(extensionName, p => new TExtension());
        _materialExtensions.Add(handleFunc);
    }

    public static (Model, List<Resources.Animation>) LoadGlbModelAndAnimations(Stream stream)
    {
        var modelRoot = ModelRoot.ReadGLB(stream, new ReadSettings { Validation = SharpGLTF.Validation.ValidationMode.TryFix });

        var model = processModelRoot(modelRoot);

        if (model.Skeleton == null)
            return (model, []);

        var animations = processAnimations(modelRoot);

        foreach(var animation in animations)
        {
            animation.Skeleton = model.Skeleton;
        }
        return (model, animations);
    }


    public static (Model, List<Resources.Animation>) LoadGlbModelAndAnimations(string filePath)
    {
        using (var streamReader = new StreamReader(filePath))
        {
            return LoadGlbModelAndAnimations(streamReader.BaseStream);
        }
    }

    public static (Model, List<Resources.Animation>) LoadGltfModelAndAnimations(string filePath)
    {
        var modelRoot = ModelRoot.Load(filePath);

        var model = processModelRoot(modelRoot);

        if (model.Skeleton == null)
            return (model, []);

        var animations = processAnimations(modelRoot);

        foreach (var animation in animations)
        {
            animation.Skeleton = model.Skeleton;
        }
        return (model, animations);
    }

    public static Model LoadGlbModel(Stream stream)
    {
        
        var modelRoot = ModelRoot.ReadGLB(stream, new ReadSettings { Validation = SharpGLTF.Validation.ValidationMode.TryFix });

        return processModelRoot(modelRoot);
    }


    public static Model LoadGlbModel(string filePath)
    {
        using (var streamReader = new StreamReader(filePath))
        {
            return LoadGlbModel(streamReader.BaseStream);
        }
    }

    public static Model LoadGltfModel(string filePath)
    {
        var modelRoot = ModelRoot.Load(filePath);

        return processModelRoot(modelRoot);

    }

    public static List<Resources.Animation> LoadGltfAnimations(string filePath, Skeleton? skeleton = null)
    {
        var modelRoot = ModelRoot.Load(filePath);

        if (skeleton == null)
            skeleton = processSkeleton(modelRoot);

        if (skeleton == null)
            return [];

        var animations = processAnimations(modelRoot);

        foreach(var animation in animations)
        {
            animation.Skeleton = skeleton;
        }

        return animations;
    }


    public static List<Resources.Animation> LoadGlbAnimations(string filePath, Skeleton? skeleton = null)
    {
        using (var sr = new StreamReader(filePath))
        {
            return LoadGlbAnimations(sr.BaseStream);
        }
    }
    public static List<Resources.Animation> LoadGlbAnimations(Stream stream, Skeleton? skeleton = null)
    {
        var modelRoot = ModelRoot.ReadGLB(stream, new ReadSettings { Validation = SharpGLTF.Validation.ValidationMode.TryFix });

        if (skeleton == null)
            skeleton = processSkeleton(modelRoot);

        if (skeleton == null)
            return [];

        var animations = processAnimations(modelRoot);

        foreach (var animation in animations)
        {
            animation.Skeleton = skeleton;
        }

        return animations;

    }


    private static List<Resources.Animation> processAnimations(ModelRoot modelRoot)
    {
        var list = new List<Resources.Animation>();

        foreach (var gltfAnimation in modelRoot.LogicalAnimations)
        {
            var animation = new Resources.Animation();

            animation.Name = gltfAnimation.Name;
            animation.Duration = gltfAnimation.Duration;

            foreach (var channel in gltfAnimation.Channels)
            {
                if (animation.Channels.TryGetValue(channel.TargetNode.Name, out var animationChannel) == false)
                {
                    animationChannel = new Resources.AnimationChannel();
                    animation.Channels[channel.TargetNode.Name] = animationChannel;

                }


                switch (channel.TargetNodePath)
                {
                    case PropertyPath.translation:
                        {

                            var keys = channel.GetTranslationSampler().GetLinearKeys();

                            foreach (var key in keys)
                            {
                                animationChannel.PositionKeyframes.Add(new Keyframe<Vector3>
                                {
                                    Time = key.Key,
                                    Value = key.Value,
                                });
                            }
                        }
                        break;
                    case PropertyPath.rotation:
                        {
                            var keys = channel.GetRotationSampler().GetLinearKeys();

                            foreach (var key in keys)
                            {
                                animationChannel.RotationKeyframes.Add(new Keyframe<Quaternion>
                                {
                                    Time = key.Key,
                                    Value = key.Value,
                                });
                            }
                        }
                        break;
                    case PropertyPath.scale:
                        {
                            var keys = channel.GetScaleSampler().GetLinearKeys();
                            foreach (var key in keys)
                            {
                                animationChannel.ScaleKeyframes.Add(new Keyframe<Vector3>
                                {
                                    Time = key.Key,
                                    Value = key.Value,
                                });
                            }
                        }
                        break;

                    default:
                        break;
                }

            }
        
            list.Add(animation);
        }

        return list;
    }
    private static Model processModelRoot(ModelRoot modelRoot)
    {
        Model? model = null;

        var skeleton = processSkeleton(modelRoot);

        model = new Model();

        model.Skeleton = skeleton;

        model.Name = modelRoot.DefaultScene.Name;

        Dictionary<SharpGLTF.Schema2.Texture, Texture> textureMap = new();

        Dictionary<SharpGLTF.Schema2.Material, Material> materialMap = new();

        Dictionary<MaterialChannel, Channel> channelMap = new();

        foreach (var texture in modelRoot.LogicalTextures)
        {
            if (texture.PrimaryImage != null)
            {
                var data = texture.PrimaryImage.Content.Content;
                var tex = TextureLoader.LoadTexture(data.ToArray());
                if (tex != null)
                {
                    textureMap[texture] = tex;
                }
            }
        }


        foreach (var material in modelRoot.LogicalMaterials)
        {
            if (materialMap.ContainsKey(material))
                continue;
            var mat = new Material();

            mat.AlphaCutoff = material.AlphaCutoff;
            mat.DoubleSided = material.DoubleSided;
            mat.BlendMode = material.Alpha switch
            {
                AlphaMode.OPAQUE => BlendMode.Opaque,
                AlphaMode.BLEND => BlendMode.Translucent,
                AlphaMode.MASK => BlendMode.Masked,
                _ => BlendMode.Opaque,
            };

            foreach(var func in _materialExtensions)
            {
                var tempList = func(modelRoot, material);
                mat.Channels.AddRange(tempList);
            }

            foreach (var gltfChannel in material.Channels)
            {
                if (channelMap.TryGetValue(gltfChannel, out var channel))
                {
                    mat.Channels.Add(channel);
                    continue;
                }
                channel = new Channel();
                channel.Name = gltfChannel.Key;
                if (gltfChannel.Texture != null && textureMap.ContainsKey(gltfChannel.Texture))
                {
                    channel.Texture = textureMap[gltfChannel.Texture];
                    if (channel.Name == "BaseColor")
                    {
                        var texture = (Texture)channel.Texture;

                        texture.SetIsGammaSpace(true);
                    }

                    if (channel.Name == "MetallicRoughness")
                    {

                        var texture = (Texture)channel.Texture;
                        var metallicFactor = gltfChannel.GetFactor("MetallicFactor");
                        var roughnessFactor = gltfChannel.GetFactor("RoughnessFactor");

                        int step = texture.ColorFormat == ColorFormat.RGB ? 3 : 4;
                        for(int i = 0; i < texture.Width * texture.Height * step; i += step)
                        {
                            if (texture.IsHdr == true)
                            {
                                var r = texture.HdrData[i + 2];
                                texture.HdrData[i + 2] = r * metallicFactor;
                                var g = texture.HdrData[i + 1];
                                texture.HdrData[i + 1] = g * roughnessFactor;
                            }
                            else
                            {
                                var r = texture.LdrData[i + 2];
                                texture.LdrData[i + 2] = (byte)(r * metallicFactor);

                                var g = texture.LdrData[i + 1];
                                texture.LdrData[i + 1] = (byte)(g * roughnessFactor);
                            }
                        }
                    }

                    if (gltfChannel.TextureSampler != null)
                    {
                        if (channel.Texture != null && channel.Texture is Texture texture)
                        {
                            texture.SetWarpS(gltfChannel.TextureSampler.WrapS switch
                            {
                                SharpGLTF.Schema2.TextureWrapMode.REPEAT => TextureWrapMode.Repeat,
                                SharpGLTF.Schema2.TextureWrapMode.CLAMP_TO_EDGE => TextureWrapMode.ClampToEdge,
                                SharpGLTF.Schema2.TextureWrapMode.MIRRORED_REPEAT => TextureWrapMode.MirroredRepeat,
                                _ => TextureWrapMode.Repeat,
                            });

                            texture.SetWarpT(gltfChannel.TextureSampler.WrapT switch
                            {
                                SharpGLTF.Schema2.TextureWrapMode.REPEAT => TextureWrapMode.Repeat,
                                SharpGLTF.Schema2.TextureWrapMode.CLAMP_TO_EDGE => TextureWrapMode.ClampToEdge,
                                SharpGLTF.Schema2.TextureWrapMode.MIRRORED_REPEAT => TextureWrapMode.MirroredRepeat,
                                _ => TextureWrapMode.Repeat,
                            });

                            texture.SetMinFilter(gltfChannel.TextureSampler.MinFilter switch
                            {
                                TextureMipMapFilter.NEAREST => TextureFilterMode.Nearest,
                                TextureMipMapFilter.LINEAR => TextureFilterMode.Linear,
                                _ => TextureFilterMode.Linear,
                            });


                            texture.SetMagFilter(gltfChannel.TextureSampler.MagFilter switch
                            {
                                TextureInterpolationFilter.NEAREST => TextureFilterMode.Nearest,
                                TextureInterpolationFilter.LINEAR => TextureFilterMode.Linear,
                                _ => TextureFilterMode.Linear,
                            });
                        }

                    }
                }
                else
                {

                    try
                    {
                        channel.Texture = Texture.CreateFromColor(gltfChannel.Color.ToColor());
                    }
                    catch
                    {
                    }
                }
                mat.Channels.Add(channel);
                channelMap[gltfChannel] = channel;
            }

            materialMap[material] = mat;

        }

        foreach (var node in modelRoot.DefaultScene.VisualChildren)
        {
            processNode(node, model, materialMap, skeleton);
        }


        foreach(var mesh in model.Meshes)
        {
            mesh.Model = model;
        }

        return model;
    }


    private static Skeleton? processSkeleton(ModelRoot modelRoot)
    {
        var dict = new Dictionary<SharpGLTF.Schema2.Node, Skeleton>();


        foreach (var skin in modelRoot.LogicalSkins)
        {
            if (skin.JointsCount <= 0)
                continue;

            var root = skin.Joints[0];

            var skeleton = new Skeleton();

            Dictionary<string, Bone> boneMap = new();

            for (int i = 0; i < skin.Joints.Count; i++)
            {
                var joint = skin.Joints[i];
                skeleton.Bones.Add(new Bone
                {
                    Name = joint.Name,
                    Index = i,
                    InverseWorldMatrix = joint.WorldMatrix.Inverse(),
                    LocalMatrix = joint.LocalMatrix,
                    WorldMatrix = joint.WorldMatrix,
                });

                boneMap.Add(joint.Name, skeleton.Bones.Last());
            }

            processBone(skin.Joints[0], boneMap);

            foreach(var bone in skeleton.Bones)
            {
                bone.WorldMatrix = GetWorldMatrix(bone);
                bone.InverseWorldMatrix = bone.WorldMatrix.Inverse();
            }
            skeleton.Root = boneMap[skin.Joints[0].Name];

            dict[skin.Joints[0]] = skeleton;
        }

        if (dict.Count > 0)
        {
            return dict.Values.First();
        }
        return null;
    }

    private static Matrix4x4 GetWorldMatrix(Bone bone)
    {
        if (bone.Parent == null)
            return bone.LocalMatrix;
        return bone.LocalMatrix * GetWorldMatrix(bone.Parent);
    }
    private static void processBone(SharpGLTF.Schema2.Node node, Dictionary<string, Bone> boneMap)
    {
        if (boneMap.TryGetValue(node.Name, out var bone))
        {
            foreach (var child in node.VisualChildren)
            {
                if (boneMap.TryGetValue(child.Name, out var childBone))
                {
                    bone.Children.Add(childBone);

                    childBone.Parent = bone;
                }
            }
        }

        foreach (var child in node.VisualChildren)
        {
            processBone(child, boneMap);
        }

    }
    private static void processNode(SharpGLTF.Schema2.Node node, Node parent, Dictionary<SharpGLTF.Schema2.Material, Material> materialMap, Skeleton? skeleton)
    {
        Node? currentNode = new Node();

        currentNode.Name = node.Name;

        currentNode.LocalTransform = node.LocalMatrix;

        parent.AddChild(currentNode, AttachToParentRule.KeepLocal);


        if (node.Mesh != null)
        {
            foreach (var primitive in node.Mesh.Primitives)
            {
                Mesh? mesh = null;

                mesh = new Mesh();

                mesh.LocalTransform = Matrix4x4.Identity;

                currentNode.AddChild(mesh, AttachToParentRule.KeepLocal);

                mesh.Name = node.Name;

                var geometry = new Geometry();

                foreach (var (name, accessor) in primitive.VertexAccessors)
                {
                    switch (name)
                    {
                        case "POSITION":
                            geometry.SetVertexAttribute(BuildInVertexAttribute.Position, 3, primitive.GetVertexColumns().Positions.SelectMany(v => new float[] { v.X, v.Y, v.Z }).ToList());
                            break;
                        case "TEXCOORD_0":
                            geometry.SetVertexAttribute(BuildInVertexAttribute.TexCoord_0, 2, primitive.GetVertexColumns().TexCoords0.SelectMany(v => new float[] { v.X, v.Y }).ToList());
                            break;
                        case "TEXCOORD_1":
                            geometry.SetVertexAttribute(BuildInVertexAttribute.TexCoord_1, 2, primitive.GetVertexColumns().TexCoords1.SelectMany(v => new float[] { v.X, v.Y }).ToList());
                            break;
                        case "TEXCOORD_2":
                            geometry.SetVertexAttribute(BuildInVertexAttribute.TexCoord_2, 2, primitive.GetVertexColumns().TexCoords2.SelectMany(v => new float[] { v.X, v.Y }).ToList());
                            break;
                        case "TEXCOORD_3":
                            geometry.SetVertexAttribute(BuildInVertexAttribute.TexCoord_0, 2, primitive.GetVertexColumns().TexCoords3.SelectMany(v => new float[] { v.X, v.Y }).ToList());
                            break;
                        case "NORMAL":
                            geometry.SetVertexAttribute(BuildInVertexAttribute.Normal, 3, primitive.GetVertexColumns().Normals.SelectMany(v => new float[] { v.X, v.Y, v.Z }).ToList());
                            break;
                        case "JOINTS_0":
                            if (skeleton == null)
                                break;
                            geometry.SetVertexAttribute(BuildInVertexAttribute.Jonits_0, 4, primitive.GetVertexColumns().Joints0.SelectMany(v =>
                            {
                                if (node.Skin == null)
                                    return new float[] { v.X, v.Y, v.Z, v.W };
                                var x = skeleton.Bones.Where(bone => bone.Name == node.Skin.Joints[(int)v.X].Name).First().Index;
                                var y = skeleton.Bones.Where(bone => bone.Name == node.Skin.Joints[(int)v.Y].Name).First().Index;
                                var z = skeleton.Bones.Where(bone => bone.Name == node.Skin.Joints[(int)v.Z].Name).First().Index;
                                var w = skeleton.Bones.Where(bone => bone.Name == node.Skin.Joints[(int)v.W].Name).First().Index;
                                return new float[] { x, y, z, w };
                            }).ToList());
                            break;
                        case "JOINTS_1":
                            if (skeleton == null)
                                break;
                            geometry.SetVertexAttribute(BuildInVertexAttribute.Jonits_1, 4, primitive.GetVertexColumns().Joints1.SelectMany(v =>
                            {
                                if (node.Skin == null)
                                    return new float[] { v.X, v.Y, v.Z, v.W };
                                var x = skeleton.Bones.Where(bone => bone.Name == node.Skin.Joints[(int)v.X].Name).First().Index;
                                var y = skeleton.Bones.Where(bone => bone.Name == node.Skin.Joints[(int)v.Y].Name).First().Index;
                                var z = skeleton.Bones.Where(bone => bone.Name == node.Skin.Joints[(int)v.Z].Name).First().Index;
                                var w = skeleton.Bones.Where(bone => bone.Name == node.Skin.Joints[(int)v.W].Name).First().Index;
                                return new float[] { x, y, z, w };
                            }).ToList());
                            break;
                        case "WEIGHTS_0":
                            geometry.SetVertexAttribute(BuildInVertexAttribute.Weights_0, 4, primitive.GetVertexColumns().Weights0.SelectMany(v => new float[] { v.X, v.Y, v.Z, v.W }).ToList());
                            break;
                        case "WEIGHTS_1":
                            geometry.SetVertexAttribute(BuildInVertexAttribute.Weights_1, 4, primitive.GetVertexColumns().Weights1.SelectMany(v => new float[] { v.X, v.Y, v.Z, v.W }).ToList());
                            break;
                    }
                }

                geometry.SetIndices(primitive.GetIndices().ToList());

                var normals = geometry.GetAttributeData(BuildInVertexAttribute.Normal);
                var uvs = geometry.GetAttributeData(BuildInVertexAttribute.TexCoord_0);
                if (normals != null && uvs != null)
                {
                    ModelHelper.CalcVerticsTbn(geometry.Indices, normals, uvs, out var tangents, out var bitangents);
                    geometry.SetVertexAttribute(BuildInVertexAttribute.Tangent, 3, tangents);
                    geometry.SetVertexAttribute(BuildInVertexAttribute.Bitangent, 3, bitangents);
                }

                mesh.Geometry = geometry;

                if (primitive.Material != null)
                {
                    materialMap.TryGetValue(primitive.Material, out var material);
                    mesh.Material = material;
                }
                else
                {

                }
            }
        }

        foreach (var child in node.VisualChildren)
        {
            processNode(child, currentNode, materialMap, skeleton);
        }

    }

}
