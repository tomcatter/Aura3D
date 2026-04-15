using SharpGLTF.Schema2;
using System.Numerics;

namespace Aura3D.Core.Resources;

public class AnimationGraph : IAnimationSampler
{
    public AnimationGraph(Skeleton skeleton, AnimationGraphNode root)
    {
        bonesTransform = new Matrix4x4[skeleton.Bones.Count];

        for (var i = 0; i < bonesTransform.Length; i++)
        {
            bonesTransform[i] = skeleton.Bones[i].WorldMatrix;
        }

        Root = root;
        currentNode = root;
        lastNode = currentNode;
        startTime = DateTime.Now;
    }

    public AnimationGraphNode Root;

    private AnimationGraphNode lastNode;
    private AnimationGraphNode currentNode;

    public float currentWeight = 1;
    public bool ExternalUpdate { get; set; } = false;

    private DateTime startTime { get; set; } = default;
    public IReadOnlyList<Matrix4x4> BonesTransform => bonesTransform;

    private Matrix4x4[] bonesTransform;

    public void Update(double deltaTime)
    {
        var timeSpan = DateTime.Now - startTime;
        double elapsedSeconds = timeSpan.TotalSeconds;

        if (elapsedSeconds < 0)
            elapsedSeconds = 0;

        if (timeSpan.TotalSeconds > currentNode.BlendTime)
        {
            currentWeight = 1;
        }
        else
        {
            currentWeight = (float)(elapsedSeconds / currentNode.BlendTime);

        }

        if (currentWeight < 1)
        {
            lastNode.Sampler.Update(deltaTime);
            currentNode.Sampler.Update(deltaTime);
            for(int i = 0; i < bonesTransform.Length; i++)
            {
                bonesTransform[i] = Matrix4x4.Lerp(lastNode.Sampler.BonesTransform[i], currentNode.Sampler.BonesTransform[i], currentWeight);
            }
        }
        else
        {
            currentNode.Sampler.Update(deltaTime); 
            for (int i = 0; i < bonesTransform.Length; i++)
            {
                bonesTransform[i] = currentNode.Sampler.BonesTransform[i];
            }
        }

        foreach(var (fun, nextNode) in currentNode.NextNodes)
        {
            if (fun(currentNode.Sampler, deltaTime) == true)
            {
                lastNode = currentNode;
                currentNode = nextNode;
                currentNode.Sampler.Reset();
                startTime = DateTime.Now;
                currentWeight = 0;
                break;
            }
        }

    }

    public void Reset()
    {
        currentNode = Root;
        lastNode = currentNode;
        startTime = DateTime.Now;
    }
}

public class AnimationGraphNode
{
    public AnimationGraphNode(IAnimationSampler sampler)
    {
        Sampler = sampler;
    }
    public float BlendTime { get; set; }

    public IAnimationSampler Sampler {  get; private set; }

    public void AddNextNode(Func<IAnimationSampler, double, bool> func, AnimationGraphNode node)
    {
        if (this == node)
            throw new InvalidOperationException("An animation graph node cannot reference itself as a next node.");
        NextNodes.Add((func, node));
    }

    internal List<(Func<IAnimationSampler, double, bool>, AnimationGraphNode)> NextNodes { get; private set; } = [];

}