#version 330 core

uniform vec4  u_color;                   // B: background color
uniform float u_absorption_coefficient;  // mu_a
uniform vec3  u_camera_position;
uniform mat4  u_model;

in vec3 v_world_position; // desde basic.vs
out vec4 FragColor;

struct Ray {
    vec3 origin;
    vec3 direction; // normalizada
};

// Intersección rayo-caja AABB en espacio de objeto (NO MODIFICAR)
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
    const vec3 boxMin = vec3(-0.5);
    const vec3 boxMax = vec3( 0.5);
    vec2 tHit = intersectAABB(ro, rd, boxMin, boxMax);

    float tEnter = tHit.x;
    float tExit  = tHit.y;

    // Rechazo si no hay recorrido válido hacia delante
    if (tExit < max(tEnter, 0.0)) discard;

    // 3) Espesor óptico (distancia dentro del volumen)
    float ta = max(tEnter, 0.0);
    float tb = tExit;
    float thickness = max(tb - ta, 0.0);

    // 4) Transmittance (Beer-Lambert)
    float T = exp(-u_absorption_coefficient * thickness);

    // 5) Radiancia final: L = B * T
    vec3 B = u_color.rgb;
    vec3 L = B * T;

    FragColor = vec4(L, 1.0);
}