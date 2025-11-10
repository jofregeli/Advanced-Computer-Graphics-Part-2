#version 330 core

uniform vec3  ub_color;                   // B: background color
uniform vec3  u_camera_position;
uniform mat4  u_model;
uniform vec3  u_max;
uniform vec3  u_min;
uniform float stepLength; 
uniform float choose_value1;
uniform float choose_value2;
uniform float choose_value3;
uniform float ac;


in vec3 v_world_position; // desde basic.vs
out vec4 FragColor;

struct Ray {
    vec3 origin;
    vec3 direction; // normalizada
};

// Intersección rayo-caja AABB en espacio de objeto (NO MODIFICAR)

float mod289(float x){return x - floor(x * (1.0 / 289.0)) * 289.0;}
vec4 mod289(vec4 x){return x - floor(x * (1.0 / 289.0)) * 289.0;}
vec4 perm(vec4 x){return mod289(((x * 34.0) + 1.0) * x);}

// 	<www.shadertoy.com/view/XsX3zB>
//	by Nikita Miropolskiy

/* discontinuous pseudorandom uniformly distributed in [-0.5, +0.5]^3 */
vec3 random3(vec3 c) {
	float j = 4096.0*sin(dot(c,vec3(17.0, 59.4, 15.0)));
	vec3 r;
	r.z = fract(512.0*j);
	j *= .125;
	r.x = fract(512.0*j);
	j *= .125;
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
	 
	w.x = dot(x, x);
	w.y = dot(x1, x1);
	w.z = dot(x2, x2);
	w.w = dot(x3, x3);
	 
	w = max(0.6 - w, 0.0);
	 
	d.x = dot(random3(s), x);
	d.y = dot(random3(s + i1), x1);
	d.z = dot(random3(s + i2), x2);
	d.w = dot(random3(s + 1.0), x3);
	 
	w *= w;
	w *= w;
	d *= w;
	 
	return dot(d, vec4(52.0));
}

float GetAbsorptionCoefficient(vec3 m, float c1, float c2, float c3) {
	return   abs(0.5333333* snoise(m)
				+0.2666667* snoise(2.0*m*c1)
				+0.1333333* snoise(4.0*m*c2)
				+0.0666667* snoise(8.0*m*c3));
}



vec2 intersectAABB(vec3 rayOrigin, vec3 rayDir, vec3 boxMin, vec3 boxMax) {
    vec3 tMin = (boxMin - rayOrigin) / rayDir;
    vec3 tMax = (boxMax - rayOrigin) / rayDir;
    vec3 t1 = min(tMin, tMax);
    vec3 t2 = max(tMin, tMax);
    float tNear = max(max(t1.x, t1.y), t1.z);
    float tFar = min(min(t2.x, t2.y), t2.z);
    return vec2(tNear, tFar);
};

void main()
{
    // 1) Ray en espacio MUNDO (cámara -> fragmento del proxy)
    Ray rayW;
    rayW.origin    = u_camera_position;
    rayW.direction = normalize(v_world_position - u_camera_position);

    // Transformar rayo a OBJETO para intersectar con AABB
    mat4 invM = inverse(u_model);
    vec3 ro = (invM * vec4(rayW.origin,    1.0)).xyz;
    vec3 rd = normalize((invM * vec4(rayW.direction, 0.0)).xyz);

    // 2) Intersección con AABB del volumen en OBJETO
    vec3 boxMin = u_min;
    vec3 boxMax = u_max;
    vec2 tHit = intersectAABB(ro, rd, boxMin, boxMax);

    float tEnter = tHit.x;
    float tExit  = tHit.y;

    

    // Rechazo si no hay recorrido válido hacia delante
    if (tExit < max(tEnter, 0.0)) discard;


    float t_max = tExit - tEnter; 
    float tao = 0.0;
    
    float t = stepLength/2;
    
    while(t < t_max){
        vec3 position = ro + rd * (tEnter + t);
        float u_absorption_coefficient = ac * GetAbsorptionCoefficient(position, choose_value1, choose_value2, choose_value3);

        tao += u_absorption_coefficient * stepLength;
        t += stepLength;
    }

    vec3 L = ub_color * exp(-tao);

    FragColor = vec4(L, 1.0);
    
}