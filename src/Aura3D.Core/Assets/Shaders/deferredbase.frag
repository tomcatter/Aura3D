#version 300 es
precision mediump float;
layout (location = 0) out vec4 Buffer_BaseColor_Metalness;
layout (location = 1) out vec4 Buffer_Normal_Roughness;
layout (location = 2) out vec4 Buffer_Emissive_Occlusion;

//{{defines}}

uniform sampler2D Texture_BaseColor;
uniform sampler2D Texture_Normal;
uniform sampler2D Texture_Metalness;
uniform sampler2D Texture_Roughness;
uniform sampler2D Texture_Emissive;
uniform sampler2D Texture_Occlusion;


in vec2 vTexCoord;
in vec3 vFragPosition;
in mat3 vTBN;
in vec3 vNormal; 

void main() 
{
    vec4 baseColor = texture(Texture_BaseColor, vTexCoord);
    vec3 normal = texture(Texture_Normal, vTexCoord).xyz;
    vec4 metalness = texture(Texture_Metalness, vTexCoord);
    vec4 roughness = texture(Texture_Roughness, vTexCoord);
    vec4 emissive = texture(Texture_Emissive, vTexCoord);
    vec4 occlusion = texture(Texture_Occlusion, vTexCoord);

    
    normal = normalize(normal.xyz * 2.0 - 1.0);
    normal = normalize(vTBN * normal);

    Buffer_BaseColor_Metalness = vec4(baseColor.rgb, metalness.r);
    Buffer_Normal_Roughness = vec4(normal, roughness.r);
    Buffer_Emissive_Occlusion = vec4(emissive.rgb, occlusion.r);

}