using SharpGLTF.Schema2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aura3D.Model;

public abstract class MaterialExtensionLoaderBase
{
    public abstract string Name { get; }
    public abstract void LoadMaterialExtension(ModelRoot modelRoot, SharpGLTF.Schema2.Material modelMaterial, Core.Resources.Material LogicMaterial);
}

