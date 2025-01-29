#version 330 core

layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec2 aTexCoord;

out vec2 vTexCoord;

uniform mat4 view;
uniform mat4 projection;

mat4 translation = mat4(
    1.0, 0.0, 0.0, 0.0, // Column 1
    0.0, 1.0, 0.0, 0.0, // Column 2
    0.0, 0.0, 1.0, 0.0, // Column 3
    -0.9, -0.85, 0.0, 1.0  // Column 4 (Translation)
);


void main()
{
    gl_Position = translation * projection * view * vec4(aPosition, 1.0);
    vTexCoord = aTexCoord;
}
