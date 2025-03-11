#version 450
#extension GL_EXT_debug_printf : enable
layout(location = 0) in vec2 inPosition;
layout(location = 1) in vec4 inColor;
layout(location = 2) in vec2 instancePos;
layout(location = 3) in vec4 instanceCol;
layout(location = 4) in vec2 instanceOffset;

layout(location = 0) out vec4 fragColor;

void main() {
    vec2 mulVec = normalize(instanceOffset);
    float len = length(instanceOffset);
    debugPrintfEXT("My float is %f", len);
    vec2 newPos = vec2(sign(inPosition.x)*len, inPosition.y);
    newPos = vec2(newPos.x*mulVec.x - newPos.y*mulVec.y, newPos.x*mulVec.y+newPos.y*mulVec.x);
    gl_Position = vec4(newPos+instancePos, 0.0, 1.0);
    fragColor = instanceCol;
}