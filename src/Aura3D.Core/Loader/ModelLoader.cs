using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using SharpGLTF.IO;
using SharpGLTF.Schema2;
using System;
using System.Numerics;
using Material = Aura3D.Core.Resources.Material;
using Mesh = Aura3D.Core.Nodes.Mesh;
using Node = Aura3D.Core.Nodes.Node;
using Texture = Aura3D.Core.Resources.Texture;
using TextureWrapMode = Aura3D.Core.Resources.TextureWrapMode;


namespace Aura3D.Core;

public static class ModelLoader
{

    static Dictionary<Type, Type> _materialExtensionTypes = new();
    public static void RegisterMaterialExtensions<T1, T2>() where T1: JsonSerializable where T2: MaterialExtensionLoaderBase
    {
        _materialExtensionTypes[typeof(T1)] = typeof(T2);
    }


    public static (SkinnedModel, List<Resources.Animation>) LoadGlbModelAndAnimations(Stream stream)
    {
        var modelRoot = ModelRoot.ReadGLB(stream, new ReadSettings { Validation = SharpGLTF.Validation.ValidationMode.TryFix });

        var model = processModelRoot(modelRoot);

        if (model is not SkinnedModel skinnedModel)
            throw new Exception("The model is not a skinned model.");

        var animations = processAnimations(modelRoot);

        foreach(var animation in animations)
        {
            animation.Skeleton = skinnedModel.Skeleton;
        }
        return (skinnedModel, animations);
    }


    public static (SkinnedModel, List<Resources.Animation>) LoadGlbModelAndAnimations(string filePath)
    {
        using (var streamReader = new StreamReader(filePath))
        {
            return LoadGlbModelAndAnimations(streamReader.BaseStream);
        }
    }

    public static (SkinnedModel, List<Resources.Animation>) LoadGltfModelAndAnimations(string filePath)
    {
        var modelRoot = ModelRoot.Load(filePath);

        var model = processModelRoot(modelRoot);

        if (model is not SkinnedModel skinnedModel)
            throw new Exception("The model is not a skinned model.");

        var animations = processAnimations(modelRoot);

        foreach (var animation in animations)
        {
            animation.Skeleton = skinnedModel.Skeleton;
        }
        return (skinnedModel, animations);
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

    public static List<Resources.Animation> LoadGltfAnimations(string filePath)
    {
        var modelRoot = ModelRoot.Load(filePath);

        return processAnimations(modelRoot);
    }


    public static List<Resources.Animation> LoadGlbAnimations(string filePath)
    {
        using (var sr = new StreamReader(filePath))
        {
            return LoadGlbAnimations(sr.BaseStream);
        }
    }
    public static List<Resources.Animation> LoadGlbAnimations(Stream stream)
    {
        var modelRoot = ModelRoot.ReadGLB(stream, new ReadSettings { Validation = SharpGLTF.Validation.ValidationMode.TryFix });

        return processAnimations(modelRoot);

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

        var skeletonMap = processSkeleton(modelRoot);

        if (skeletonMap.Count > 0)
        {
            var skinnedModel = new SkinnedModel();

            skinnedModel.Skeleton = skeletonMap.Values.First();

            model = skinnedModel;
        }
        else
        {
            model = new Model();
        }

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

            //foreach(var func in _materialExtensions)
            //{
            //    var tempList = func(modelRoot, material);
            //    mat.Channels.AddRange(tempList);
            //}

            foreach(var ext in material.Extensions)
            {
                var type = ext.GetType();

                _materialExtensionTypes.TryGetValue(ext.GetType(), out Type extType);
                if (extType == null)
                    continue;
                MaterialExtensionLoaderBase materialExtension = (MaterialExtensionLoaderBase)Activator.CreateInstance(extType);
                if(materialExtension == null)
                    continue;
                materialExtension.LoadMaterialExtension(modelRoot, material, mat);
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
                try
                {
                    channel.Color = gltfChannel.Color.ToColor();
                }
                catch
                {
                }
                if (gltfChannel.Texture != null && textureMap.ContainsKey(gltfChannel.Texture))
                {
                    channel.Texture = textureMap[gltfChannel.Texture];
                    if (channel.Name == "BaseColor")
                    {
                        var texture = (Texture)
                        channel.Texture;

                        texture.SetIsGammaSpace(true);
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
                mat.Channels.Add(channel);
                channelMap[gltfChannel] = channel;
            }

            materialMap[material] = mat;

        }

        foreach (var node in modelRoot.DefaultScene.VisualChildren)
        {
            processNode(node, model, materialMap, skeletonMap);
        }


        foreach(var mesh in model.Meshes)
        {
            mesh.Model = model;

            if (mesh is SkinnedMesh skinnedMesh && model is SkinnedModel skinnedModel)
            {
                skinnedMesh.SkinnedModel = skinnedModel;
            }
        }

        return model;
    }


    private static Dictionary<SharpGLTF.Schema2.Node, Skeleton> processSkeleton(ModelRoot modelRoot)
    {
        var dict = new Dictionary<SharpGLTF.Schema2.Node, Skeleton>();


        foreach (var skin in modelRoot.LogicalSkins)
        {
            if (skin.Skeleton == null)
                continue;

            if (dict.ContainsKey(skin.Skeleton))
                continue;

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
            processBone(skin.Skeleton, boneMap);

            foreach(var bone in skeleton.Bones)
            {
                bone.WorldMatrix = GetWorldMatrix(bone);
                bone.InverseWorldMatrix = bone.WorldMatrix.Inverse();
            }
            skeleton.Root = boneMap[skin.Skeleton.Name];

            dict[skin.Skeleton] = skeleton;
        }

        return dict;
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
    private static void processNode(SharpGLTF.Schema2.Node node, Node parent, Dictionary<SharpGLTF.Schema2.Material, Material> materialMap, Dictionary<SharpGLTF.Schema2.Node, Skeleton> skeletonMap)
    {
        Node? currentNode = new Node();

        currentNode.Name = node.Name;

        parent.AddChild(currentNode);

        currentNode.LocalTransform = node.LocalMatrix;

        if (node.Mesh != null)
        {
            foreach (var primitive in node.Mesh.Primitives)
            {
                Mesh? mesh = null;

                if (node.Skin != null && node.Skin.Skeleton != null)
                {
                    var skinnedMesh = new SkinnedMesh();

                    if (skeletonMap.TryGetValue(node.Skin.Skeleton, out var skeleton))
                    {
                        skinnedMesh.Skeleton = skeleton;
                    }
                    mesh = skinnedMesh;
                }
                else
                {
                    mesh = new Mesh();
                }

                currentNode.AddChild(mesh);

                mesh.LocalTransform = Matrix4x4.Identity;

                mesh.Name = node.Name;

                var geometry = new Geometry();

                foreach (var (name, accessor) in primitive.VertexAccessors)
                {
                    switch (name)
                    {
                        case "POSITION":
                            geometry.SetVertexAttribute(BuildInVertexAttribute.Position, primitive.GetVertexColumns().Positions.SelectMany(v => new float[] { v.X, v.Y, v.Z }).ToList());
                            break;
                        case "TEXCOORD_0":
                            geometry.SetVertexAttribute(BuildInVertexAttribute.TexCoord, primitive.GetVertexColumns().TexCoords0.SelectMany(v => new float[] { v.X, v.Y }).ToList());
                            break;
                        case "NORMAL":
                            geometry.SetVertexAttribute(BuildInVertexAttribute.Normal, primitive.GetVertexColumns().Normals.SelectMany(v => new float[] { v.X, v.Y, v.Z }).ToList());
                            break;
                        case "JOINTS_0":
                            if (skeletonMap.Count == 0)
                                break;
                            geometry.SetVertexAttribute(BuildInVertexAttribute.BoneIndices, primitive.GetVertexColumns().Joints0.SelectMany(v =>
                            {
                                if (node.Skin == null)
                                    return new float[] { v.X, v.Y, v.Z, v.W };
                                var x = skeletonMap.First().Value.Bones.Where(bone => bone.Name == node.Skin.Joints[(int)v.X].Name).First().Index;
                                var y = skeletonMap.First().Value.Bones.Where(bone => bone.Name == node.Skin.Joints[(int)v.X].Name).First().Index;
                                var z = skeletonMap.First().Value.Bones.Where(bone => bone.Name == node.Skin.Joints[(int)v.X].Name).First().Index;
                                var w = skeletonMap.First().Value.Bones.Where(bone => bone.Name == node.Skin.Joints[(int)v.X].Name).First().Index;
                                return new float[] { x, y, z, w };
                            }).ToList());
                            break;
                        case "WEIGHTS_0":
                            if (skeletonMap.Count == 0)
                                break;
                            geometry.SetVertexAttribute(BuildInVertexAttribute.BoneWeights, primitive.GetVertexColumns().Weights0.SelectMany(v => new float[] { v.X, v.Y, v.Z, v.W }).ToList());
                            break;
                    }
                }

                geometry.SetIndices(primitive.GetIndices().ToList());

                var normal = geometry.GetAttributeData(BuildInVertexAttribute.Normal);
                var uv = geometry.GetAttributeData(BuildInVertexAttribute.TexCoord);
                if (normal != null && uv != null)
                {
                    ModelHelper.CalcVerticsTbn(geometry.Indices, normal, uv, out var tangents, out var bitangents);
                    geometry.SetVertexAttribute(BuildInVertexAttribute.Tangent, tangents);
                    geometry.SetVertexAttribute(BuildInVertexAttribute.Bitangent, bitangents);
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
            processNode(child, currentNode, materialMap, skeletonMap);
        }

    }

}
