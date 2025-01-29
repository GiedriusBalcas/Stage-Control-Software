#version 330 core

in vec4 vertexColor;
in vec4 clipSpacePos; // Received from vertex shader

out vec4 FragColor;

void main()
{
    // Perform perspective division to get NDC coordinates
    vec3 ndc = clipSpacePos.xyz / clipSpacePos.w;

    // Calculate the distance from the fragment to the closest edge in NDC
    float distX = 1.0 - abs(ndc.x);
    float distY = 1.0 - abs(ndc.y);
    float edgeDist = min(distX, distY);

    // Define the width of the fade effect in NDC space (0.0 to 1.0)
    float fadeWidth = 0.2;

    // Compute the alpha value based on edge distance
    float alpha = clamp(edgeDist / fadeWidth * vertexColor.a, 0.0, 1);

    // Set the fragment color with the computed alpha
    FragColor = vec4(vertexColor.rgb, alpha);
}
