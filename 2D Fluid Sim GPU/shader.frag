#version 330 core
out vec4 FragColor;
in vec2 TexCoord;

uniform sampler2D velocityTex;

void main()
{
    vec2 velocity = texture(velocityTex, TexCoord).rg;
    float mag = length(velocity);

    // Clamp magnitude between 0 and 1
    float t = clamp(mag/2, 0.0, 1.0);

    // Define blue and red colors
    vec3 blue = vec3(0.0, 0.0, 1.0);
    vec3 red = vec3(1.0, 0.0, 0.0);
    vec3 lblue = vec3(0.4, 1.0, 1.0);
    vec3 dblue = vec3(0.2, 0.6, 1.0);
    
    // Linear interpolate between blue and red by t
    vec3 color = mix(dblue, lblue, t);

    FragColor = vec4(color, 1.0);
}

