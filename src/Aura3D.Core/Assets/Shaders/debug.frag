#version 300 es
precision mediump float;
out vec4 outColor;

//{{defines}}

uniform vec3 uColor;

void main()
{
    outColor = vec4(uColor, 1.0);
}
