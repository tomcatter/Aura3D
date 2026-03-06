using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpGLTF.Schema2;
using System.Numerics;
using JSONREADER = System.Text.Json.Utf8JsonReader;
using JSONWRITER = System.Text.Json.Utf8JsonWriter;
using System.Runtime.CompilerServices;



namespace Aura3D.Core;

public class CelMaterialExtensionLoader : MaterialExtensionLoaderBase
{
    public override string Name => "AURA3D_TEXTURES_CELSHADING";

    [ModuleInitializer]
    internal static void Init()
    {
        // Register Extension
        ModelLoader.RegisterMaterialExtensions<Aura3DCelExtraProperties, CelMaterialExtensionLoader>();
    }

    private static Resources.Texture? GetTextureAtIndex(ModelRoot modelRoot, int index)
    {
        if (index < 0 || index > modelRoot.LogicalTextures.Count)
            return null;
        SharpGLTF.Schema2.Texture glTexture = modelRoot.LogicalTextures[index];

        var data = glTexture.PrimaryImage.Content.Content;
        var tex = TextureLoader.LoadTexture(data.ToArray());
        return tex;
    }

    public override void LoadMaterialExtension(ModelRoot modelRoot, SharpGLTF.Schema2.Material modelMaterial, Resources.Material logicMaterial)
    {
        // Read Texture
        foreach (var extension in modelMaterial.Extensions)
        {
            if (!(extension.GetType() == typeof(Aura3DCelExtraProperties)))
                continue;

            Aura3DCelExtraProperties celExt = (Aura3DCelExtraProperties)extension;
            // var ILMTexture = GetTextureAtIndex(modelRoot, celExt.ILM);

            string[] texturesNames = { "ILM", "SDF", "ShadowRamp", "SpecularRamp" };
            int i = 0;
            foreach (int textureIdx in new int[] { celExt.ILM, celExt.SDF, celExt.ShadowRamp, celExt.SpecularRamp })
            {
                var texture = GetTextureAtIndex(modelRoot, textureIdx);
                if (texture == null)
                {
                    ++i;
                    continue;
                }
                var channel = new Resources.Channel();
                channel.Texture = texture;
                channel.Name = texturesNames[i];
                logicMaterial.Channels.Add(channel);
                ++i;
            }
            logicMaterial.SetParameterValue<float>("_RampIndex0", celExt.rampIndex0);
            logicMaterial.SetParameterValue<float>("_RampIndex1", celExt.rampIndex1);
            logicMaterial.SetParameterValue<float>("_RampIndex2", celExt.rampIndex2);
            logicMaterial.SetParameterValue<float>("_RampIndex3", celExt.rampIndex3);
            logicMaterial.SetParameterValue<float>("_RampIndex4", celExt.rampIndex4);

            logicMaterial.SetParameterValue<float>("_BrightFac", celExt.brightFac);
            logicMaterial.SetParameterValue<float>("_GreyFac", celExt.greyFac);
            logicMaterial.SetParameterValue<float>("_DarkFac", celExt.darkFac);
            logicMaterial.SetParameterValue<float>("_BrightAreaShadowFac", celExt.brightAreaShadowFac);

            logicMaterial.SetParameterValue<Vector4>("_BrightAreaShadowFac", celExt.lightAreaColorTint);
            logicMaterial.SetParameterValue<Vector4>("_DarkShadowColor", celExt.darkShadowColor);
            logicMaterial.SetParameterValue<Vector4>("_CoolDarkShadowColor", celExt.coolDarkShadowColor);

            logicMaterial.SetParameterValue<float>("_FaceShadowOffset", celExt.faceShadowOffset);
            logicMaterial.SetParameterValue<float>("_FaceShadowTransitionSoftness", celExt.faceShadowTransitionSoftness);

            break;
        }
    }

}

public class Aura3DCelExtraProperties : ExtraProperties
{
    public string Name => SCHEMANAME;

    public new const string SCHEMANAME = "AURA3D_TEXTURES_CELSHADING";
    protected override string GetSchemaName() => SCHEMANAME;

    public Aura3DCelExtraProperties() { }

    //private static readonly List<string> proppertyNames = new List<string>{ "ILM", "SDF", "ShadowRamp", "SpecularRamp",
    //    "_RampIndex0", "_RampIndex1", "_RampIndex2", "_RampIndex3", "_RampIndex4",
    //    "_BrightFac", "_GreyFac", "_DarkFac", "_BrightAreaShadowFac", 
    //    "_BrightAreaShadowFac", "_DarkShadowColor", "_CoolDarkShadowColor",
    //    "_FaceShadowOffset", "_FaceShadowTransitionSoftness"
    //};

    [ModuleInitializer]
    internal static void Init()
    {
        // Register Extension
        SharpGLTF.Schema2.ExtensionsFactory.RegisterExtension<SharpGLTF.Schema2.Material, Aura3DCelExtraProperties>(SCHEMANAME, _ => new Aura3DCelExtraProperties());
    }

    #region data

    public int ILM;

    public int SDF;

    public int ShadowRamp;

    public int SpecularRamp;

    // Ramp Index：
    public float rampIndex0;
    public float rampIndex1;
    public float rampIndex2;
    public float rampIndex3;
    public float rampIndex4;

    // Light Factor
    public float brightFac;
    public float greyFac;
    public float darkFac;
    public float brightAreaShadowFac;

    // Color Tint
    public Vector4 lightAreaColorTint;
    public Vector4 darkShadowColor;
    public Vector4 coolDarkShadowColor;

    // SDF Offset
    public float faceShadowOffset;
    public float faceShadowTransitionSoftness;

    #endregion

    private static Resources.Texture? GetTextureAtIndex(ModelRoot modelRoot, int index)
    {
        if (index < 0 || index > modelRoot.LogicalTextures.Count)
            return null;
        SharpGLTF.Schema2.Texture glTexture = modelRoot.LogicalTextures[index];

        var data = glTexture.PrimaryImage.Content.Content;
        var tex = TextureLoader.LoadTexture(data.ToArray());
        return tex;
    }

    public static List<Resources.Channel> GetExtenionChannels(ModelRoot modelRoot, SharpGLTF.Schema2.Material material)
    {
        List<Resources.Channel> channels = new List<Resources.Channel>();
        foreach (var extension in material.Extensions)
        {
            if (extension.GetType() == typeof(Aura3DCelExtraProperties))
            {
                Aura3DCelExtraProperties celExt = (Aura3DCelExtraProperties)extension;
                var ILMTexture = GetTextureAtIndex(modelRoot, celExt.ILM);

                string[] texturesNames = { "ILM", "SDF", "ShadowRamp", "SpecularRamp" };
                int i = 0;
                foreach (int textureIdx in new int[] { celExt.ILM, celExt.SDF, celExt.ShadowRamp, celExt.SpecularRamp })
                {
                    var texture = GetTextureAtIndex(modelRoot, textureIdx);
                    if (texture == null)
                    {
                        ++i;
                        continue;
                    }
                    var channel = new Resources.Channel();
                    channel.Texture = texture;
                    channel.Name = texturesNames[i];
                    channels.Add(channel);
                    ++i;
                }
            }
        }

        return channels;
    }

    #region serialization

    protected override void SerializeProperties(JSONWRITER writer)
    {
        base.SerializeProperties(writer);

        SerializeProperty(writer, "ILM", ILM);
        SerializeProperty(writer, "SDF", SDF);
        SerializeProperty(writer, "ShadowRamp", ShadowRamp);
        SerializeProperty(writer, "SpecularRamp", SpecularRamp);

        SerializeProperty(writer, "_RampIndex0", rampIndex0);
        SerializeProperty(writer, "_RampIndex1", rampIndex1);
        SerializeProperty(writer, "_RampIndex2", rampIndex2);
        SerializeProperty(writer, "_RampIndex3", rampIndex3);
        SerializeProperty(writer, "_RampIndex4", rampIndex4);

        SerializeProperty(writer, "_BrightFac", brightFac);
        SerializeProperty(writer, "_GreyFac", greyFac);
        SerializeProperty(writer, "_DarkFac", darkFac);
        SerializeProperty(writer, "_BrightAreaShadowFac", brightAreaShadowFac);

        SerializeProperty(writer, "_LightAreaColorTint", lightAreaColorTint);
        SerializeProperty(writer, "_DarkShadowColor", darkShadowColor);
        SerializeProperty(writer, "_CoolDarkShadowColor", coolDarkShadowColor);

        SerializeProperty(writer, "_FaceShadowOffset", faceShadowOffset);
        SerializeProperty(writer, "_FaceShadowTransitionSoftness", faceShadowTransitionSoftness);
    }

    protected override void DeserializeProperty(string jsonPropertyName, ref JSONREADER reader)
    {
        switch (jsonPropertyName)
        {
            case "ILM": DeserializePropertyValue<Aura3DCelExtraProperties, int>(ref reader, this, out ILM); break;
            case "SDF": DeserializePropertyValue<Aura3DCelExtraProperties, int>(ref reader, this, out SDF); break;
            case "ShadowRamp": DeserializePropertyValue<Aura3DCelExtraProperties, int>(ref reader, this, out ShadowRamp); break;
            case "SpecularRamp": DeserializePropertyValue<Aura3DCelExtraProperties, int>(ref reader, this, out SpecularRamp); break;

            case "_RampIndex0": DeserializePropertyValue<Aura3DCelExtraProperties, float>(ref reader, this, out rampIndex0); break;
            case "_RampIndex1": DeserializePropertyValue<Aura3DCelExtraProperties, float>(ref reader, this, out rampIndex1); break;
            case "_RampIndex2": DeserializePropertyValue<Aura3DCelExtraProperties, float>(ref reader, this, out rampIndex2); break;
            case "_RampIndex3": DeserializePropertyValue<Aura3DCelExtraProperties, float>(ref reader, this, out rampIndex3); break;
            case "_RampIndex4": DeserializePropertyValue<Aura3DCelExtraProperties, float>(ref reader, this, out rampIndex4); break;

            case "_BrightFac": DeserializePropertyValue<Aura3DCelExtraProperties, float>(ref reader, this, out brightFac); break;
            case "_GreyFac": DeserializePropertyValue<Aura3DCelExtraProperties, float>(ref reader, this, out greyFac); break;
            case "_DarkFac": DeserializePropertyValue<Aura3DCelExtraProperties, float>(ref reader, this, out darkFac); break;
            case "_BrightAreaShadowFac": DeserializePropertyValue<Aura3DCelExtraProperties, float>(ref reader, this, out brightAreaShadowFac); break;

            case "_LightAreaColorTint": DeserializePropertyValue<Aura3DCelExtraProperties, Vector4>(ref reader, this, out lightAreaColorTint); break;
            case "_DarkShadowColor": DeserializePropertyValue<Aura3DCelExtraProperties, Vector4>(ref reader, this, out darkShadowColor); break;
            case "_CoolDarkShadowColor": DeserializePropertyValue<Aura3DCelExtraProperties, Vector4>(ref reader, this, out coolDarkShadowColor); break;

            case "_FaceShadowOffset": DeserializePropertyValue<Aura3DCelExtraProperties, float>(ref reader, this, out faceShadowOffset); break;
            case "_FaceShadowTransitionSoftness": DeserializePropertyValue<Aura3DCelExtraProperties, float>(ref reader, this, out faceShadowTransitionSoftness); break;

            default: base.DeserializeProperty(jsonPropertyName, ref reader); break;
        }
    }

    #endregion
}
