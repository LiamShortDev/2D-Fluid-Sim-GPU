#version 330 core
layout(location = 0) in vec2 aPosition;
layout(location = 1) in float aMagnitude;

out float magnitude;

void main()
{
    magnitude = aMagnitude;  // pass to fragment shader
    gl_Position = vec4(aPosition, 0.0, 1.0);
}
