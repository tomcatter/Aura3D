#version 300 es
precision mediump float;

//{{defines}}

#define BONE_NUMBER 100


layout(location = 0) in vec3 position;
layout(location = 1) in vec2 texCoord;
layout(location = 2) in vec3 normal;
layout(location = 3) in vec3 tangent;
layout(location = 4) in vec3 bitangent;

#ifdef SKINNED_MESH
layout(location = 5) in vec4 boneIndices;
layout(location = 6) in vec4 boneWeights;

uniform mat4 BoneMatrices[BONE_NUMBER];

#endif

uniform mat4 modelMatrix;
uniform mat4 viewMatrix;
uniform mat4 projectionMatrix;
uniform mat4 normalMatrix;
uniform mat4 normalPrjMatrix;

uniform float outlineWidth;
uniform float widthOffset;

out vec2 vTexCoord;
out vec3 vFragPosition;
out mat3 vTBN;
out vec3 vNormal;
out vec3 debugLineColor;


void main()
{
	vTexCoord = texCoord;

    vec3 T = normalize(mat3(normalMatrix) * tangent);
    vec3 B = normalize(mat3(normalMatrix) * bitangent);
    vec3 N = normalize(mat3(normalMatrix) * normal); 
	mat3 TBN = mat3(T, B, N);
	vTBN = TBN;
	vNormal =  N;
	vec3 positionOS = position;
	vec3 positionOS_Offset2 = position + outlineWidth  * 0.001 * normal;

#ifdef SKINNED_MESH
		
	mat4 skinMatrix = boneWeights.x * BoneMatrices[int(boneIndices.x)];
	skinMatrix += boneWeights.y * BoneMatrices[int(boneIndices.y)];
	skinMatrix += boneWeights.z * BoneMatrices[int(boneIndices.z)];
	skinMatrix += boneWeights.w * BoneMatrices[int(boneIndices.w)];

	vec4 worldPosition = modelMatrix * skinMatrix * vec4(positionOS, 1.0);
	vec4 worldPosition_Offset2 = modelMatrix * skinMatrix * vec4(positionOS_Offset2, 1.0);

#else
	vec4 worldPosition = modelMatrix * vec4(positionOS, 1.0);
	vec4 worldPosition_Offset2 = modelMatrix * vec4(positionOS_Offset2, 1.0); 
#endif
	

	vec3 normalPrj = mat3(normalPrjMatrix) * normal;
	vec3 normalNormalPrj = normalize(normalPrj);

	vec4 outVertex = projectionMatrix * viewMatrix * worldPosition;
	vec4 outVertex_Offset2 = projectionMatrix * viewMatrix * worldPosition_Offset2;

	vec3 normalOffset1 = normalNormalPrj.xyz * outlineWidth * 0.001 * outVertex.w;
	// normalOffset1.z = 0.00001f;


	vec3 normalOffset2 = (outVertex_Offset2 - outVertex).xyz;

	float len1 = length(normalOffset1);
	float len2 = length(normalOffset2);

	if (len1 < len2) {
		outVertex.xyz += normalOffset1;
		debugLineColor = vec3(1.0f, 0.0f, 0.0f);
	} 
	else {
		// normalOffset2.z = 0.00001f;
		outVertex.xyz += normalOffset2;
		debugLineColor = vec3(0.0f, 1.0f, 0.0f);

	}

	//outVertex.xyz += normalOffset1;

	vFragPosition = worldPosition.xyz;
	gl_Position = outVertex;

}