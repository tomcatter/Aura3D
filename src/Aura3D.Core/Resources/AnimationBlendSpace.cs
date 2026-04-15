using Aura3D.Core.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Aura3D.Core.Resources;

public class AnimationBlendSpace : IAnimationSampler
{
    public void Reset()
    {
        AxisValue = new(0, 0);
    }
    public AnimationBlendSpace(Skeleton skeleton)
    {
        Skeleton = skeleton;

        bonesTransform = new Matrix4x4[skeleton.Bones.Count];

        for (var i = 0; i < bonesTransform.Length; i++)
        {
            bonesTransform[i] = skeleton.Bones[i].WorldMatrix;
        }
    }

    public Skeleton Skeleton { get; private set; }

    public bool ExternalUpdate { get; set; } = false;

    public IReadOnlyList<Matrix4x4> BonesTransform => bonesTransform;

    public Matrix4x4[] bonesTransform;

    List <(Vector2, IAnimationSampler)> animationSamplers = [];

    List<float> weights = new List<float>();

    public void AddAnimationSampler(Vector2 point, IAnimationSampler animationSampler)
    {
        if (point.X > 1 || point.X < -1)
            throw new ArgumentOutOfRangeException(nameof(point), "Animation sampler point X must be in range [-1, 1].");

        if (point.Y > 1 || point.Y < -1)
            throw new ArgumentOutOfRangeException(nameof(point), "Animation sampler point Y must be in range [-1, 1].");

        animationSamplers.Add((point, animationSampler));
        weights.Add(0);

    }
    Vector2 AxisValue = default;

    public void SetAxis(float x, float y)
    {
        if (x < -1 || y < -1 || x > 1 || y > 1)
            throw new ArgumentOutOfRangeException(nameof(x), "Axis values must be in range [-1, 1].");
        AxisValue.X = x;
        AxisValue.Y = y;
    }
    public float IdwPower { get; set; } = 2f;

    public void Update(double deltaTime)
    {
        float totalRawWeight = 0f;

        int index = 0;
        foreach (var (point, anim) in animationSamplers)
        {
            float distance = CalculateDistance(AxisValue.X, AxisValue.Y, point.X, point.Y);
            
            if (distance < 0.000001)
            {
                anim.Update(deltaTime);

                for(int i = 0; i < BonesTransform.Count; i++)
                {
                    bonesTransform[i] = anim.BonesTransform[i];
                }
                return;
            }
            weights[index] = 1f / (float)MathF.Pow(distance, IdwPower);
            totalRawWeight += weights[index];

            index++;
        }

        index = 0;
        for (int i = 0; i < weights.Count; i ++)
        {
            float weight = weights[i] / totalRawWeight;
            if (weight < 0.0001)
                weight = 0;
            if (weight > 0.9999)
                weight = 1;
            weights[i] = weight;
        }
        
        index = 0;
        foreach (var weight in weights)
        {
            if (weight > 0)
            {
                animationSamplers[index].Item2.Update(deltaTime);
                for (int j = 0; j < BonesTransform.Count; j++)
                {
                    if (index == 0)
                        bonesTransform[j] = animationSamplers[index].Item2.BonesTransform[j] * weight;
                    else
                        bonesTransform[j] = bonesTransform[j] + animationSamplers[index].Item2.BonesTransform[j] * weight;
                }
            }
            index++;
        }

    }

    private float CalculateDistance(float x1, float y1, float x2, float y2)
    {
        float dx = x1 - x2;
        float dy = y1 - y2;
        return (float)MathF.Sqrt(dx * dx + dy * dy);
    }
}
