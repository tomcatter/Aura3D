#version 300 es
precision highp float;
//{{defines}}

#define ENABLE_DIR_LIGHT 0
#define ENABLE_POINT_LIGHT 0
#define ENABLE_SPOT_LIGHT 1

in vec2 TexCoords;

layout (location = 0) out vec4 FragColor;

uniform sampler2D gBufferBaseColorMetalness;
uniform sampler2D gBufferNormalRoughness;
uniform sampler2D depthTexture;

uniform mat4 invProjection;
uniform mat4 invView;

uniform vec3 viewPos;

#if ENABLE_DIR_LIGHT
uniform vec3 dirLightDirection;
uniform vec3 dirLightColor;
uniform float dirLightIntensity;
#endif

#if ENABLE_POINT_LIGHT
uniform vec3 pointLightPosition;
uniform vec3 pointLightColor;
uniform float pointLightIntensity;
uniform float pointLightConstant;
uniform float pointLightLinear;
uniform float pointLightQuadratic;
#endif

#if ENABLE_SPOT_LIGHT
uniform vec3 spotLightPosition;
uniform vec3 spotLightDirection;
uniform vec3 spotLightColor;
uniform float spotLightIntensity;
uniform float spotLightConstant;
uniform float spotLightLinear;
uniform float spotLightQuadratic;
uniform float spotLightCutOff;
uniform float spotLightOuterCutOff;
#endif

const float PI = 3.14159265359;

vec3 reconstructWorldPosFromDepth(vec2 texCoords) {
    float depth = texture(depthTexture, texCoords).r;
    vec3 ndc;
    ndc.xy = texCoords * 2.0 - 1.0;
    ndc.z = depth * 2.0 - 1.0;
    vec4 clipPos = vec4(ndc, 1.0);
    vec4 viewPos = invProjection * clipPos;
    viewPos /= viewPos.w;
    vec4 worldPos = invView * viewPos;
    return worldPos.xyz;
}

float DistributionGGX(vec3 N, vec3 H, float roughness) {
    float a = roughness * roughness;
    float a2 = a * a;
    float NdotH = max(dot(N, H), 0.0);
    float NdotH2 = NdotH * NdotH;
    float nom   = a2;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = PI * denom * denom;
    return nom / max(denom, 1e-7);
}

float GeometrySchlickGGX(float NdotV, float roughness) {
    float r = (roughness + 1.0);
    float k = (r * r) / 8.0;
    float nom   = NdotV;
    float denom = NdotV * (1.0 - k) + k;
    return nom / max(denom, 1e-7);
}

float GeometrySmith(vec3 N, vec3 V, vec3 L, float roughness) {
    float NdotV = max(dot(N, V), 0.0);
    float NdotL = max(dot(N, L), 0.0);
    float ggx2 = GeometrySchlickGGX(NdotV, roughness);
    float ggx1 = GeometrySchlickGGX(NdotL, roughness);
    return ggx1 * ggx2;
}

vec3 fresnelSchlick(float cosTheta, vec3 F0) {
    return F0 + (1.0 - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

#if ENABLE_DIR_LIGHT
vec3 calcSingleDirLight(vec3 N, vec3 V, vec3 fragPos, vec3 albedo, float metalness, float roughness) {
    vec3 L = normalize(-dirLightDirection);
    vec3 H = normalize(V + L);
    vec3 F0 = mix(vec3(0.04), albedo, metalness);

    float NDF = DistributionGGX(N, H, roughness);
    float G = GeometrySmith(N, V, L, roughness);
    vec3 F = fresnelSchlick(max(dot(H, V), 0.0), F0);

    vec3 specular = (NDF * G * F) / max(4.0 * max(dot(N, V), 0.0) * max(dot(N, L), 0.0), 1e-7);
    vec3 kS = F;
    vec3 kD = (vec3(1.0) - kS) * (1.0 - metalness);
    vec3 diffuse = kD * albedo / PI;

    float NdotL = max(dot(N, L), 0.0);
    return (diffuse + specular) * dirLightColor * dirLightIntensity * NdotL;
}
#endif

#if ENABLE_POINT_LIGHT
vec3 calcSinglePointLight(vec3 N, vec3 V, vec3 fragPos, vec3 albedo, float metalness, float roughness) {
    vec3 L = normalize(pointLightPosition - fragPos);
    float distance = length(pointLightPosition - fragPos);
    float attenuation = 1.0 / (pointLightConstant + pointLightLinear * distance + pointLightQuadratic * (distance * distance));
    
    vec3 H = normalize(V + L);
    vec3 F0 = mix(vec3(0.04), albedo, metalness);

    float NDF = DistributionGGX(N, H, roughness);
    float G = GeometrySmith(N, V, L, roughness);
    vec3 F = fresnelSchlick(max(dot(H, V), 0.0), F0);

    vec3 specular = (NDF * G * F) / max(4.0 * max(dot(N, V), 0.0) * max(dot(N, L), 0.0), 1e-7);
    vec3 kS = F;
    vec3 kD = (vec3(1.0) - kS) * (1.0 - metalness);
    vec3 diffuse = kD * albedo / PI;

    float NdotL = max(dot(N, L), 0.0);
    vec3 radiance = pointLightColor * pointLightIntensity * attenuation;
    return (diffuse + specular) * radiance * NdotL;
}
#endif

#if ENABLE_SPOT_LIGHT
vec3 calcSingleSpotLight(vec3 N, vec3 V, vec3 fragPos, vec3 albedo, float metalness, float roughness) {
    vec3 L = normalize(spotLightPosition - fragPos);
    float distance = length(spotLightPosition - fragPos);
    float distanceAttenuation = 1.0 / (spotLightConstant + spotLightLinear * distance + spotLightQuadratic * (distance * distance));
    
    float theta = dot(L, normalize(-spotLightDirection));
    float epsilon = spotLightCutOff - spotLightOuterCutOff;
    float angleAttenuation = clamp((theta - spotLightOuterCutOff) / epsilon, 0.0, 1.0);
    
    vec3 H = normalize(V + L);
    vec3 F0 = mix(vec3(0.04), albedo, metalness);

    float NDF = DistributionGGX(N, H, roughness);
    float G = GeometrySmith(N, V, L, roughness);
    vec3 F = fresnelSchlick(max(dot(H, V), 0.0), F0);

    vec3 specular = (NDF * G * F) / max(4.0 * max(dot(N, V), 0.0) * max(dot(N, L), 0.0), 1e-7);
    vec3 kS = F;
    vec3 kD = (vec3(1.0) - kS) * (1.0 - metalness);
    vec3 diffuse = kD * albedo / PI;

    float totalAttenuation = distanceAttenuation * angleAttenuation;
    float NdotL = max(dot(N, L), 0.0);
    vec3 radiance = spotLightColor * spotLightIntensity * totalAttenuation;
    
    return (diffuse + specular) * radiance * NdotL;
}
#endif

void main() {
    vec4 baseColorMetal = texture(gBufferBaseColorMetalness, TexCoords);
    vec3 albedo = pow(baseColorMetal.rgb, vec3(2.2));
    float metalness = baseColorMetal.a;

    vec4 normalRough = texture(gBufferNormalRoughness, TexCoords);
    vec3 N = normalize(normalRough.rgb * 2.0 - 1.0);
    float roughness = clamp(normalRough.a, 0.01, 0.99);

    vec3 fragPosWorld = reconstructWorldPosFromDepth(TexCoords);
    vec3 V = normalize(viewPos - fragPosWorld);

    vec3 lightContribution = vec3(0.0);
    #if ENABLE_DIR_LIGHT
    lightContribution = calcSingleDirLight(N, V, fragPosWorld, albedo, metalness, roughness);
    #endif
    #if ENABLE_POINT_LIGHT
    lightContribution = calcSinglePointLight(N, V, fragPosWorld, albedo, metalness, roughness);
    #endif
    #if ENABLE_SPOT_LIGHT
    lightContribution = calcSingleSpotLight(N, V, fragPosWorld, albedo, metalness, roughness);
    #endif

    FragColor = vec4(lightContribution, 1.0);
}