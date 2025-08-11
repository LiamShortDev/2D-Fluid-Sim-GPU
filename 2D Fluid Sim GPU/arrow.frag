#version 330 core
in float magnitude;
out vec4 FragColor;

vec3 hsv2rgb(vec3 c) {
    vec3 rgb = clamp(
        abs(mod(c.x * 6.0 + vec3(0.0, 4.0, 2.0), 6.0) - 3.0) - 1.0,
        0.0,
        1.0
    );
    return c.z * mix(vec3(1.0), rgb, c.y);  
}

void main()
{
    float t = clamp(magnitude, 0.0, 1.0);
    float hue = mix(0.3333, 0.0, t); // green to red
    vec3 hsv = vec3(hue, 1.0, 1.0);
    vec3 rgb = hsv2rgb(hsv);
    FragColor = vec4(rgb, 1.0);
}
