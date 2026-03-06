#version 300 es
precision mediump float;
out vec4 outColor;

const float brightnessLevels[16] = float[](0.7, 0.85, 0.85, 0.85, 0.85, 0.85, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0);
const float baseAmbient = 0.;

//{{defines}}

#define MAX_DIRECTIONAL_LIGHTS 4
#define MAX_POINT_LIGHTS 4
#define MAX_SPOT_LIGHTS 4


#define DL_SHADOW_ASSIGN(index) if (DirectionalLights[index].castShadow == 1.0) \
	shadows[index] = CalculateShadow(DirectionalLights[index].shadowMapMatrix, DirectionalLightShadowMaps[index]); \
	else \
	shadows[index] = 1.0;

#define PL_SHADOW_ASSIGN(index) if (PointLights[index].castShadow == 1.0) \
	shadows[index] = CalculatePointLightShadow(PointLights[index].position, PointLights[index].shadowMapMatrices, PointLightShadowMaps[index]); \
		else \
	shadows[index] = 1.0;

#define SP_SHADOW_ASSIGN(index) if (SpotLights[index].castShadow == 1.0) \
	shadows[index] = CalculateShadow(SpotLights[index].shadowMapMatrix, SpotLightShadowMaps[index]); \
	else \
	shadows[index] = 1.0;

#define REPEAT_DL_SHADOW_ASSIGN_1 DL_SHADOW_ASSIGN(0)
#define REPEAT_DL_SHADOW_ASSIGN_2 REPEAT_DL_SHADOW_ASSIGN_1; DL_SHADOW_ASSIGN(1)
#define REPEAT_DL_SHADOW_ASSIGN_3 REPEAT_DL_SHADOW_ASSIGN_2; DL_SHADOW_ASSIGN(2)
#define REPEAT_DL_SHADOW_ASSIGN_4 REPEAT_DL_SHADOW_ASSIGN_3; DL_SHADOW_ASSIGN(3)
#define REPEAT_DL_SHADOW_ASSIGN_5 REPEAT_DL_SHADOW_ASSIGN_4; DL_SHADOW_ASSIGN(4)
#define REPEAT_DL_SHADOW_ASSIGN_6 REPEAT_DL_SHADOW_ASSIGN_5; DL_SHADOW_ASSIGN(5)
#define REPEAT_DL_SHADOW_ASSIGN_7 REPEAT_DL_SHADOW_ASSIGN_6; DL_SHADOW_ASSIGN(6)
#define REPEAT_DL_SHADOW_ASSIGN_8 REPEAT_DL_SHADOW_ASSIGN_7; DL_SHADOW_ASSIGN(7)
#define REPEAT_DL_SHADOW_ASSIGN_9 REPEAT_DL_SHADOW_ASSIGN_8; DL_SHADOW_ASSIGN(8)
#define REPEAT_DL_SHADOW_ASSIGN_10 REPEAT_DL_SHADOW_ASSIGN_9; DL_SHADOW_ASSIGN(9)


#define REPEAT_PL_SHADOW_ASSIGN_1 PL_SHADOW_ASSIGN(0)
#define REPEAT_PL_SHADOW_ASSIGN_2 REPEAT_PL_SHADOW_ASSIGN_1; PL_SHADOW_ASSIGN(1)
#define REPEAT_PL_SHADOW_ASSIGN_3 REPEAT_PL_SHADOW_ASSIGN_2; PL_SHADOW_ASSIGN(2)
#define REPEAT_PL_SHADOW_ASSIGN_4 REPEAT_PL_SHADOW_ASSIGN_3; PL_SHADOW_ASSIGN(3)
#define REPEAT_PL_SHADOW_ASSIGN_5 REPEAT_PL_SHADOW_ASSIGN_4; PL_SHADOW_ASSIGN(4)
#define REPEAT_PL_SHADOW_ASSIGN_6 REPEAT_PL_SHADOW_ASSIGN_5; PL_SHADOW_ASSIGN(5)
#define REPEAT_PL_SHADOW_ASSIGN_7 REPEAT_PL_SHADOW_ASSIGN_6; PL_SHADOW_ASSIGN(6)
#define REPEAT_PL_SHADOW_ASSIGN_8 REPEAT_PL_SHADOW_ASSIGN_7; PL_SHADOW_ASSIGN(7)
#define REPEAT_PL_SHADOW_ASSIGN_9 REPEAT_PL_SHADOW_ASSIGN_8; PL_SHADOW_ASSIGN(8)
#define REPEAT_PL_SHADOW_ASSIGN_10 REPEAT_PL_SHADOW_ASSIGN_9; PL_SHADOW_ASSIGN(9)


#define REPEAT_SP_SHADOW_ASSIGN_1 SP_SHADOW_ASSIGN(0)
#define REPEAT_SP_SHADOW_ASSIGN_2 REPEAT_SP_SHADOW_ASSIGN_1; SP_SHADOW_ASSIGN(1)
#define REPEAT_SP_SHADOW_ASSIGN_3 REPEAT_SP_SHADOW_ASSIGN_2; SP_SHADOW_ASSIGN(2)
#define REPEAT_SP_SHADOW_ASSIGN_4 REPEAT_SP_SHADOW_ASSIGN_3; SP_SHADOW_ASSIGN(3)
#define REPEAT_SP_SHADOW_ASSIGN_5 REPEAT_SP_SHADOW_ASSIGN_4; SP_SHADOW_ASSIGN(4)
#define REPEAT_SP_SHADOW_ASSIGN_6 REPEAT_SP_SHADOW_ASSIGN_5; SP_SHADOW_ASSIGN(5)
#define REPEAT_SP_SHADOW_ASSIGN_7 REPEAT_SP_SHADOW_ASSIGN_6; SP_SHADOW_ASSIGN(6)
#define REPEAT_SP_SHADOW_ASSIGN_8 REPEAT_SP_SHADOW_ASSIGN_7; SP_SHADOW_ASSIGN(7)
#define REPEAT_SP_SHADOW_ASSIGN_9 REPEAT_SP_SHADOW_ASSIGN_8; SP_SHADOW_ASSIGN(8)
#define REPEAT_SP_SHADOW_ASSIGN_10 REPEAT_SP_SHADOW_ASSIGN_9; SP_SHADOW_ASSIGN(9)



struct s_directional_light_info
{
	vec3 color;
	vec3 direction;	
	mat4 shadowMapMatrix;
	float castShadow;
};

struct s_point_light_info
{
	vec3 color;
	vec3 position;
	float radius; 
	float castShadow;
	mat4 shadowMapMatrices[6];
};

struct s_spot_light_info
{
	vec3 color;
	vec3 position;
	vec3 direction;
	float radius; 
	float inner_cone_cos;
	float outer_cone_cos;
	float castShadow;
	mat4 shadowMapMatrix;
};


in vec2 vTexCoord;
in vec3 vFragPosition;
in mat3 vTBN;
in vec3 vNormal;

//Texture flags 
// Bit value represent texture exist or not, start from low side: BaseColorTexture | NormalTexture | ILMTextures | ShadowRamp | SpecularRamp
uniform int TexturesFlags;

// SDF
uniform sampler2D SDFTextures;

// ILM
uniform sampler2D ILMTextures;

// Ramp 
uniform sampler2D ShadowRamp;
uniform sampler2D SpecularRamp;
uniform int _UseCoolShadowColorOrTex;
uniform float _RampIndex0;
uniform float _RampIndex1;
uniform float _RampIndex2;
uniform float _RampIndex3;
uniform float _RampIndex4;
uniform float _BrightFac;
uniform float _GreyFac;
uniform float _DarkFac;

//Shadow
uniform vec4 _LightAreaColorTint;
uniform float _FaceShadowOffset;
uniform vec4 _DarkShadowColor;
uniform vec4 _CoolDarkShadowColor;
uniform float _BrightAreaShadowFac;
uniform float _FaceShadowTransitionSoftness;

uniform sampler2D BaseColorTexture;

uniform sampler2D NormalTexture;

uniform vec4 BaseColor;

uniform float ambientIntensity;

uniform vec3 cameraPosition;

#if defined(BLENDMODE_MASKED) || defined(BLENDMODE_TRANSLUCENT)

uniform float alphaCutoff;

#endif

uniform s_directional_light_info DirectionalLights[MAX_DIRECTIONAL_LIGHTS];
uniform s_point_light_info PointLights[MAX_POINT_LIGHTS];
uniform s_spot_light_info SpotLights[MAX_SPOT_LIGHTS];


uniform sampler2D DirectionalLightShadowMaps[MAX_DIRECTIONAL_LIGHTS];
uniform samplerCube PointLightShadowMaps[MAX_POINT_LIGHTS];
uniform sampler2D SpotLightShadowMaps[MAX_SPOT_LIGHTS];

float mapToDiscreteLevels(float value) {
    int index = int(value * 15.0);
    index = clamp(index, 0, 15);
    return brightnessLevels[index];
}


const int BaseColorBit = 1;
const int NormalBit = 2;
const int ILMBit = 4;
const int SDFBit = 8;
const int ShadowRampBit = 16;
const int SpecularRampBit = 32;

// Calculate shadow attenuation with normal-based lighting and ambient occlusion
float GetShadow(vec3 normalWS, vec3 lightDirection, float aoFactor, float shadowAttenuation)
{
    float NDotL = dot(normalWS, lightDirection);
    // Apply smoothstep to create soft transition for half Lambert
    float halfLambert = smoothstep(0.0, _GreyFac, NDotL + _DarkFac);
    // Combine half Lambert with AO and shadow attenuation
    float shadow = clamp(2.0 * halfLambert * aoFactor, 0.0, 1.0) * shadowAttenuation;
    // Return 1.0 when AO factor is high (>= 0.9), otherwise return calculated shadow
    return mix(shadow, 1.0, step(0.9, aoFactor));
	// return aoFactor;
}

// Calculate shadow ramp color using half Lambert and lightmap data
vec3 GetShadowRampColor(float halfLambert, vec4 lightmap)
{
    // Create bright mask for areas above brightness threshold
    float brightMask = step(_BrightFac, halfLambert);
    
    // Determine day or night mode
    float rampSampling = 0.0;
    if(_UseCoolShadowColorOrTex == 1) { 
        rampSampling = 0.5; 
    }
    
    // Calculate ramp sampling positions based on ramp indices
    float ramp0 = _RampIndex0 * -0.1 + 1.05 - rampSampling;  // 0.95
    float ramp1 = _RampIndex1 * -0.1 + 1.05 - rampSampling;  // 0.65
    float ramp2 = _RampIndex2 * -0.1 + 1.05 - rampSampling;  // 0.75
    float ramp3 = _RampIndex3 * -0.1 + 1.05 - rampSampling;  // 0.55
    float ramp4 = _RampIndex4 * -0.1 + 1.05 - rampSampling;  // 0.85
    
    // Separate lightmap.a into different material regions
    float lightmapA1 = step(0.0, lightmap.a);   // 0.0
    float lightmapA2 = step(0.25, lightmap.a);  // 0.3
    float lightmapA3 = step(0.45, lightmap.a);  // 0.5
    float lightmapA4 = step(0.65, lightmap.a);  // 0.7
    float lightmapA5 = step(0.95, lightmap.a);  // 1.0
    
    // Reconstruct lightmap.a by blending ramp values
    float rampV = 0.0;
    rampV = mix(rampV, ramp0, lightmapA1);  // 0.0
    rampV = mix(rampV, ramp1, lightmapA2);  // 0.3
    rampV = mix(rampV, ramp2, lightmapA3);  // 0.5
    rampV = mix(rampV, ramp3, lightmapA4);  // 0.7
    rampV = mix(rampV, ramp4, lightmapA5);  // 1.0
    
    // Sample ramp texture using halfLambert as U and rampV as V coordinate
    vec3 rampCol = texture(ShadowRamp, vec2(halfLambert, rampV)).rgb;
    
    // Apply bright mask to blend between ramp color and halfLambert
    vec3 shadowRamp = mix(rampCol, vec3(halfLambert), brightMask);
    
    return shadowRamp;
}

void main()
{
	// BaseColor
	vec4 baseColor = BaseColor;
	if ((TexturesFlags & BaseColorBit) != 0)
	{
		baseColor = texture(BaseColorTexture, vTexCoord);
	}
	#if defined(BLENDMODE_MASKED) || defined(BLENDMODE_TRANSLUCENT)
		if (baseColor.a <= alphaCutoff)
			discard;
	#endif

	// Normal
 	vec3 normal = vNormal;
 	if ((TexturesFlags & NormalBit) != 0)
 	{
 		normal = texture(NormalTexture, vTexCoord).xyz;
 		normal = normalize(normal * 2.0 - 1.0);
 		normal = normalize(vTBN * normal);
 	}
	if (!gl_FrontFacing) 
	{
		normal = -normal;
	}

	// Main Light Direction
	vec3 mainLightDirection = vec3(0.25 * 3.14, 0, 0);
	vec3 mainLightColor = vec3(1.0);
	if(MAX_DIRECTIONAL_LIGHTS > 0){
		mainLightColor = DirectionalLights[0].color;
		mainLightDirection = -DirectionalLights[0].direction;
	}

	// ILM Preprocess
	bool HasILMTexture = (TexturesFlags & ILMBit) != 0;
	vec4 ilmTexCol = vec4(1.0, 1.0, 1.0, 1.0);
	if(HasILMTexture)
	{
		ilmTexCol = texture(ILMTextures, vTexCoord);
	}
	float aoFactor = ilmTexCol.y;
	float shadow = GetShadow(normal, mainLightDirection, aoFactor, 1.0);
	
	// Diffuse
	vec3 diffuseColor = vec3(0);
	vec3 rampTexCol = GetShadowRampColor(shadow, ilmTexCol);
	vec3 brightAreaColor = rampTexCol * _LightAreaColorTint.rgb;
	vec3 darkShadowColor = rampTexCol * mix(_DarkShadowColor.rgb, _CoolDarkShadowColor.rgb, float(_UseCoolShadowColorOrTex));
    vec3 ShadowColorTint = mix(darkShadowColor.rgb, brightAreaColor, _BrightAreaShadowFac);
	diffuseColor = ShadowColorTint * baseColor.xyz;

	// diffuseColor = vec3(shadow, shadow, shadow);
	// diffuseColor = normal;

	vec3 ambient = ambientIntensity * baseColor.xyz;
	vec3 finalColor = diffuseColor;

//	if (!gl_FrontFacing) 
//	{
//		finalColor = vec3(1.0, 0.0, 0.0);
//	}
//	else{
//		finalColor = vec3(0.0, 1.0, 0.0);
//	}


#ifdef BLENDMODE_TRANSLUCENT

	outColor = vec4(finalColor, baseColor.a);
#else

	outColor = vec4(finalColor, 1.0);
#endif
	
}