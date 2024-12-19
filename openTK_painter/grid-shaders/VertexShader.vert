#version 330 core

layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec4 aColor;

out vec4 vertexColor;
out vec4 clipSpacePos; // Pass clip space position to fragment shader

uniform mat4 projection;
uniform mat4 view;

void main()
{
    // Calculate world position (assuming model matrix is identity)
    vec4 worldPosition = vec4(aPosition, 1.0);

    // Calculate clip space position without the view matrix
    clipSpacePos = projection * view * worldPosition;

    // Compute final position with view matrix for correct rendering
    gl_Position = projection * view * worldPosition;

    // Pass the vertex color
    vertexColor = aColor;
}
