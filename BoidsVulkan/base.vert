#version 450
//#extension GL_EXT_debug_printf : enable
layout(location = 0) in vec2 inPosition;
layout(location = 1) in vec4 inColor;
layout(location = 2) in vec2 instancePos;
layout(location = 3) in vec4 instanceCol;
layout(location = 4) in vec2 instanceOffset;

layout(location = 0) out vec4 fragColor;

layout(push_constant) uniform PushConstants {
    vec2 xrange;
    vec2 yrange;
}push;
void main() {
    vec2 dpos = vec2((push.xrange.y - push.xrange.x), (push.yrange.y - push.yrange.x));
    vec2 offset = instanceOffset/dpos*2.0;
    vec2 pos = vec2(instancePos.x -push.xrange.x, instancePos.y - push.yrange.x)/dpos*2.0+vec2(-1, -1);
    vec2 mulVec = normalize(offset);
    float len = length(offset);
    //debugPrintfEXT("%f %f", dpos.x, dpos.y);
    vec2 newPos = vec2(sign(inPosition.x)*len, inPosition.y);
    newPos = vec2(newPos.x*mulVec.x - newPos.y*mulVec.y, newPos.x*mulVec.y+newPos.y*mulVec.x);
    gl_Position = vec4(newPos+pos, 0.0, 1.0);
    fragColor = instanceCol;
}