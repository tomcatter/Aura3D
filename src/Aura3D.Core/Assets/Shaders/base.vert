#version 300 es
precision mediump float;

#define BONE_NUMBER 150

#define MAX_DIRECTIONAL_LIGHTS 4
#define MAX_POINT_LIGHTS 4
#define MAX_SPOT_LIGHTS 4

//{{defines}}

layout(location = 0) in vec3 position;
layout(location = 1) in vec2 texCoord;
layout(location = 2) in vec4 color;
layout(location = 3) in vec3 normal;
layout(location = 4) in vec3 tangent;
layout(location = 5) in vec3 bitangent;
layout(location = 6) in vec4 boneIndices;
layout(location = 7) in vec4 boneWeights;


#ifdef INSTANCED_MESH
layout(location = 8) in mat4 modelMatrix;
layout(location = 12) in mat4 normalMatrix;
#endif


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


void main()
{
	vTexCoord = texCoord;

    vec3 T = normalize(mat3(normalMatrix) * tangent);
    vec3 B = normalize(mat3(normalMatrix) * bitangent);
    vec3 N = normalize(mat3(normalMatrix) * normal); 
	mat3 TBN = mat3(T, B, N);
	vTBN = TBN;


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