#version 330 core

// --- Standard Uniforms ---
uniform vec3  ub_color;                   
uniform vec3  u_camera_position;
uniform mat4  u_model;
uniform vec3  u_min;    // AABB Min
uniform vec3  u_max;    // AABB Max
uniform float stepLength; 

// --- Noise / Procedural Uniforms ---
uniform float choose_value1;
uniform float choose_value2;
uniform float choose_value3;
uniform float ac; // Absorption Coefficient

// --- TASK 1: Volume Data Uniforms ---
uniform sampler3D u_volume_texture; 

// --- TASK 3.2: Scattering Uniforms ---
uniform float sc;                   // Scattering Coefficient
uniform int   u_light_steps;        // Shadow ray steps

// --- LIGHT UNIFORMS ---
uniform float u_light_intensity;       
uniform vec4  u_light_color;           
uniform vec3  u_light_position;        // World Space
uniform vec3  u_local_light_position;  // Object Space 

// --- Emission Uniforms ---
uniform float le;
uniform vec4 u_emission_color;
uniform bool use_random_le;

in vec3 v_world_position; 
out vec4 FragColor;

const float PI = 3.14159265359;

struct Ray {
    vec3 origin;
    vec3 direction; 
};

// --- NOISE FUNCTIONS (Standard) ---
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

// --- DENSITY FUNCTION ---
float GetDensity(vec3 pos) {
    float density = 0.0;
    vec3 uvw = (pos - u_min) / (u_max - u_min);
    // Boundary check
    if(uvw.x >= 0.0 && uvw.x <= 1.0 &&
       uvw.y >= 0.0 && uvw.y <= 1.0 &&
       uvw.z >= 0.0 && uvw.z <= 1.0) {              
            density = texture(u_volume_texture, uvw).r;
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

// --- TASK 3.2: INNER INTEGRAL (Shadow Ray) ---
// "Compute Ls(t')... requires a new ray-marching to the existing light sources" [cite: 43]
float getLightTransmittance(vec3 pos) {
    // 1. Init ray to light source [cite: 41]
    vec3 toLight = u_local_light_position - pos;
    float distToLight = length(toLight);
    vec3 lightDir = normalize(toLight);
    
    // 2. Find intersection values (Back intersection) [cite: 42]
    vec2 tHit = intersectAABB(pos, lightDir, u_min, u_max);
    float tExit = tHit.y; 

    // We march until we hit the volume wall OR the light itself
    float marchDist = min(tExit, distToLight);
    if (marchDist <= 0.0) return 1.0; 

    float stepSize = marchDist / float(u_light_steps);
    float t_curr = 0.0;
    float opticalDepth = 0.0;

    // 3. Imitate the integral as a while loop 
    // "Accumulate an second optical thickness value" [cite: 44]
    while(t_curr < marchDist) {
        vec3 samplePos = pos + lightDir * (t_curr + 0.5 * stepSize);
        
        float d = GetDensity(samplePos);
        
        if(d > 0.0) {
             // Extinction coefficient mu_t = mu_a + mu_s
            float mu_t = ac + sc;
            opticalDepth += d * mu_t * stepSize;
        }
        
        t_curr += stepSize;
    }
    
    // Transmittance T = exp(-opticalDepth)
    return exp(-opticalDepth);
}

// --- MAIN SHADER LOOP ---
void main() {
    Ray rayW;
    rayW.origin    = u_camera_position;
    rayW.direction = normalize(v_world_position - u_camera_position);

    mat4 invM = inverse(u_model);
    vec3 ro = (invM * vec4(rayW.origin, 1.0)).xyz;
    vec3 rd = normalize((invM * vec4(rayW.direction, 0.0)).xyz);
    
    vec2 tHit = intersectAABB(ro, rd, u_min, u_max);
    float tEnter = tHit.x;
    float tExit  = tHit.y;

    if (tExit < max(tEnter, 0.0)) discard;

    float t = max(tEnter, 0.0); 
    float t_end = tExit;
    float T = 1.0; 
    vec3 L = vec3(0.0);

    t += stepLength * 0.5; 

    // "The new term takes place inside the integral so we can reuse our already existing loop" 
    while(t < t_end) {
        vec3 pos = ro + rd * t;
        float density = GetDensity(pos);

        if(density > 0.001) {
            // Get coefficients at this position [cite: 44]
            float mu_a = ac * density;
            float mu_s = sc * density;
            float mu_t = mu_a + mu_s;

            // Emission
            vec3 L_emission = u_emission_color.rgb * mu_a; 

            // --- COMPUTE Ls(t) (In-Scattering) ---
            // 4. "Use the obtained mu_s and Ls to our computation of the color" [cite: 46]
            vec3 L_s = vec3(0.0);
            
            // Calculate L_i (Incoming Light)
            vec3 toLight = u_local_light_position - pos;
            float dist = length(toLight);
            float attenuation = 1.0 / (1.0 + dist * dist);
            
            // Get Transmittance along light ray (The result of Step 3)
            float T_light = getLightTransmittance(pos);
            
            // Isotropic Phase Function: f_x = 1 / 4pi [cite: 51]
            float f_x = 1.0 / (4.0 * PI);
            
            // L_i(x, w_i)
            vec3 L_i = u_light_color.rgb * u_light_intensity * T_light * attenuation;
            
            // Combine terms for Eq (2): L_s = Integral(f_x * L_i) * mu_s
            // (Since it's a point light, the integral integral collapses to one direction)
            L_s = L_i * f_x * mu_s;

            // --- Accumulate ---
            // Combine Scattering and Emission
            vec3 S = L_emission + L_s; 
            
            L += T * S * stepLength;
            T *= exp(-mu_t * stepLength);
        }

        if(T < 0.001) break; 
        t += stepLength;
    }

    L += T * ub_color;
    FragColor = vec4(L, 1.0);
}