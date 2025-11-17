#version 330 core

// --- Standard Uniforms ---
uniform vec3  ub_color;                   
uniform vec3  u_camera_position;
uniform mat4  u_model;
uniform vec3  u_min;    // AABB Min (Required for Task 1 Coordinate Mapping)
uniform vec3  u_max;    // AABB Max (Required for Task 1 Coordinate Mapping)
uniform float stepLength; 

// --- Noise / Procedural Uniforms ---
uniform float choose_value1;
uniform float choose_value2;
uniform float choose_value3;
uniform float ac; // Absorption Coefficient

// --- TASK 1: Volume Data Uniforms ---
uniform sampler3D u_volume_texture; // The loaded VDB texture 
uniform int       u_source_type;    // 0 = Noise, 1 = VDB File 

// --- TASK 3.2: Scattering Uniforms ---
uniform float sc;               // Scattering Coefficient
uniform vec3  u_light_pos;      // Light position
uniform vec3  u_light_color;    // Light color
uniform float u_light_intensity;// Light intensity
uniform int   u_light_steps;    // Shadow ray steps

// --- Emission Uniforms ---
uniform float le;
uniform vec3 u_emission_color;
uniform bool use_random_le;

in vec3 v_world_position; 
out vec4 FragColor;

struct Ray {
    vec3 origin;
    vec3 direction; 
};

// --- NOISE FUNCTIONS (For Source Type 0) ---
vec3 random3(vec3 c) {
	float j = 4096.0*sin(dot(c,vec3(17.0, 59.4, 15.0)));
    vec3 r;
	r.z = fract(512.0*j); j *= .125;
	r.x = fract(512.0*j); j *= .125;
	r.y = fract(512.0*j);
	return r-0.5;
}

const float F3 =  0.3333333;
const float G3 =  0.1666667;

float snoise(vec3 p) {
	vec3 s = floor(p + dot(p, vec3(F3)));
	vec3 x = p - s + dot(s, vec3(G3));
    vec3 e = step(vec3(0.0), x - x.yzx);
	vec3 i1 = e*(1.0 - e.zxy);
    vec3 i2 = 1.0 - e.zxy*(1.0 - e);
	vec3 x1 = x - i1 + G3;
    vec3 x2 = x - i2 + 2.0*G3;
	vec3 x3 = x - 1.0 + 3.0*G3;
	vec4 w, d;
    w.x = dot(x, x); w.y = dot(x1, x1); w.z = dot(x2, x2); w.w = dot(x3, x3);
    w = max(0.6 - w, 0.0);
	d.x = dot(random3(s), x);
	d.y = dot(random3(s + i1), x1);
    d.z = dot(random3(s + i2), x2);
	d.w = dot(random3(s + 1.0), x3);
	w *= w * w;
    d *= w;
	return dot(d, vec4(52.0));
}

// --- TASK 1 IMPL: GetDensity ---
// Handles switching between Noise (Source 0) and VDB Texture (Source 1)
float GetDensity(vec3 pos) {
    float density = 0.0;

    //  Manipulate volume source (Noise vs VDB)
    if (u_source_type == 0) {
        // --- OPTION A: Procedural Noise ---
        density = abs(0.533*snoise(pos) 
                    + 0.267*snoise(2.0*pos*choose_value1) 
                    + 0.133*snoise(4.0*pos*choose_value2) 
                    + 0.067*snoise(8.0*pos*choose_value3));
    } 
    else if (u_source_type == 1) {
        // --- OPTION B: VDB Texture Sampling (Task 1) ---
        
        //  Change of coordinates: Object Space -> Texture Space (0 to 1)
        vec3 uvw = (pos - u_min) / (u_max - u_min);
        
        // Safety check: Ensure we only sample inside the texture box
        // (Although Ray Marching AABB intersection usually handles this, floating point errors can occur)
        if(uvw.x >= 0.0 && uvw.x <= 1.0 &&
           uvw.y >= 0.0 && uvw.y <= 1.0 &&
           uvw.z >= 0.0 && uvw.z <= 1.0) {
             
             //  Get texture value at sample position
             density = texture(u_volume_texture, uvw).r;
        }
    }
    
    return max(density, 0.0);
}

// --- AABB INTERSECTION ---
vec2 intersectAABB(vec3 rayOrigin, vec3 rayDir, vec3 boxMin, vec3 boxMax) {
    vec3 tMin = (boxMin - rayOrigin) / rayDir;
    vec3 tMax = (boxMax - rayOrigin) / rayDir;
    vec3 t1 = min(tMin, tMax);
    vec3 t2 = max(tMin, tMax);
    float tNear = max(max(t1.x, t1.y), t1.z);
    float tFar = min(min(t2.x, t2.y), t2.z);
    return vec2(tNear, tFar);
}

// --- SHADOW RAY (Task 3.2 Logic) ---
float getLightTransmittance(vec3 pos) {
    vec3 lightDir = normalize(u_light_pos - pos);
    vec2 tHit = intersectAABB(pos, lightDir, u_min, u_max);
    float tExit = tHit.y; 

    if (tExit <= 0.0) return 1.0; 

    float stepSize = tExit / float(u_light_steps);
    float opticalDepth = 0.0;

    for(int i = 0; i < u_light_steps; i++) {
        vec3 samplePos = pos + lightDir * (float(i) + 0.5) * stepSize;
        
        // Reuse GetDensity so shadows work for both Noise and VDB
        float d = GetDensity(samplePos);
        
        opticalDepth += d * (ac + sc) * stepSize;
    }
    
    return exp(-opticalDepth);
}

// --- MAIN SHADER LOOP ---
void main() {
    // 1. Setup Ray in Object Space
    Ray rayW;
    rayW.origin    = u_camera_position;
    rayW.direction = normalize(v_world_position - u_camera_position);

    mat4 invM = inverse(u_model);
    vec3 ro = (invM * vec4(rayW.origin, 1.0)).xyz;
    vec3 rd = normalize((invM * vec4(rayW.direction, 0.0)).xyz);
    
    vec3 lightPos_obj = (invM * vec4(u_light_pos, 1.0)).xyz;

    // 2. Intersect Volume AABB
    vec2 tHit = intersectAABB(ro, rd, u_min, u_max);
    float tEnter = tHit.x;
    float tExit  = tHit.y;

    if (tExit < max(tEnter, 0.0)) discard;

    // 3. Ray Marching
    float t = max(tEnter, 0.0); 
    float t_end = tExit;
    float T = 1.0; 
    vec3 L = vec3(0.0);

    // Jitter to reduce banding
    t += stepLength * 0.5; 

    while(t < t_end) {
        vec3 pos = ro + rd * t;
        
        // Sample Density (Task 1 or Noise depending on uniform)
        float density = GetDensity(pos);

        if(density > 0.001) {
            float mu_a = ac * density;
            float mu_s = sc * density;
            float mu_t = mu_a + mu_s;

            // Emission
            float emission_intensity = use_random_le ? max(snoise(pos*3.0), 0.0) : 1.0;
            vec3 L_emission = u_emission_color * emission_intensity * mu_a;

            // Scattering (Task 3.2)
            vec3 lightDir = normalize(lightPos_obj - pos);
            float T_light = getLightTransmittance(pos);
            float Phase = 1.0 / (4.0 * 3.14159); // Isotropic
            vec3 Li = u_light_color * u_light_intensity * T_light;
            vec3 L_scattering = Li * Phase * mu_s;

            // Accumulate
            vec3 S = L_emission + L_scattering; 
            L += T * S * stepLength;
            T *= exp(-mu_t * stepLength);
        }

        if(T < 0.001) break; 
        t += stepLength;
    }

    L += T * ub_color;
    FragColor = vec4(L, 1.0);
}