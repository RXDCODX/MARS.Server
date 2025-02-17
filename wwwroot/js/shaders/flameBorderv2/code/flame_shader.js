const vertex_shader_source = `#version 300 es

in vec2 aVertexPosition;
in vec2 aTexPosition;
out vec2 tex_pos;

uniform mat4 projection_matrix;
uniform mat4 model_matrix;

void main(void) 
{
    gl_Position = projection_matrix * model_matrix * vec4(aVertexPosition.xy, 0, 1);
    tex_pos = aTexPosition;
}
`;

//***************************************************************************** */



const fragment_shader_source = `#version 300 es

precision mediump float;

uniform vec2 iResolution;
uniform vec2 object_size;
uniform float iTime;
uniform float time_effect;
uniform float flame_width;
uniform float flame_height;
uniform float sigma;
uniform float luma_min;
uniform float luma_min_smooth;
uniform bool is_mirror;
uniform bool is_reflected;
uniform float alpha_percentage;
uniform bool is_apply_to_alpha_layer;
uniform vec2 noise_texture_size;
uniform sampler2D noise_texture;
uniform vec3 flame_multi_color;

uniform float aspect_ratio;

in vec2 tex_pos;
out vec4 fragColor;

//*****************************************************************************
//                                  CONSTANTS
//*****************************************************************************

const float lumaMin = 0.01;
const float lumaMinSmooth = 0.01;

//*****************************************************************************



//*****************************************************************************
//                              FUNCTION DECLARATIONS
//*****************************************************************************

float noise(vec3 p);
float fbm4(vec3 p);
float fbm6(vec3 p);
float grid(vec2 p);
vec4 create_color(vec2 input_coords, vec2 draw_area, vec2 screen_area);
vec4 create_colors(vec2 input_coords);
vec4 render(vec2 input_coords);
vec4 apply_to_alpha_layer(vec4 background_color, vec4 input_color, 
    float alpha);

float gradient(float value, float min, float max, float height);

//*****************************************************************************



//*****************************************************************************
//                                  MAIN
//*****************************************************************************

float g_time = 0.0;

void main()
{
    g_time = iTime * time_effect;

    vec2 input_coords = tex_pos * object_size;

    fragColor = render(input_coords);
}

//*****************************************************************************



//*****************************************************************************
//                              FUNCTION IMPLEMENTATIONS
//*****************************************************************************

float noise(vec3 p)
{
	// приводим координаты p к диапазону текстуры
    vec2 tex_coords = p.xy / noise_texture_size;
    
    // сэмплируем текстуру шума и возвращаем значение
    return texture(noise_texture, tex_coords).r;
}

//*****************************************************************************



//*****************************************************************************

//шум Фурье (Fractal Brownian Motion) 4 октавы
float fbm4(vec3 x) 
{
	float v = 0.0;
	float a = 0.5;

	vec3 shift = vec3(100);

	for (int i = 0; i < 4; ++i) 
	{
		v += a * noise(x);
		x = x * 2.0 + shift;
		a *= 0.5;
	}
	return v;
}

//*****************************************************************************



//*****************************************************************************

//шум Фурье (Fractal Brownian Motion) 6 октав
float fbm6(vec3 x) 
{
	float v = 0.0;
	float a = 0.5;

	vec3 shift = vec3(100);

	for (int i = 0; i < 6; ++i) 
	{
		v += a * noise(x);
		x = x * 2.0 + shift;
		a *= 0.5;
	}
	return v;
}

//*****************************************************************************



//*****************************************************************************

float grid(vec2 p) 
{
    p = sin(p * 3.1415);
    return smoothstep(-0.01, 0.01, p.x * p.y);
}

//*****************************************************************************



//*****************************************************************************

vec4 create_color(vec2 input_coords, vec2 draw_area, vec2 screen_area)
{ 
    vec2 q = input_coords / draw_area;

    vec2 p = -1.0 + 2.0 * q;
    p.x *= screen_area.x / screen_area.y;
    p.y *= 0.3;
    p.y -= g_time * 1.5;

    float tc = g_time * 1.2;
    float tw1 = g_time * 2.5;
    float tw2 = g_time * 0.6;
    
    vec3 vw1 = vec3(p, tw1);
    vw1.y *= 2.8;
    vec2 ofs1 = vec2(fbm4(vw1), fbm4(vw1 + vec3(10.0, 20.0, 50.0)));
    ofs1.y *= 0.3;
    ofs1.x *= 1.3;

    vec3 vw2 = vec3(p, tw2);
    vw2.y *= 0.8;
    vec2 ofs2 = vec2(fbm4(vw2), fbm4(vw2 + vec3(10.0, 20.0, 50.0)));
    ofs2.y *= 0.3;
    ofs2.x *= 1.3;
    
    vec2 vs = (p + ofs1 * 0.5 + ofs2 * 0.9) * 4.0;
    vec3 vc = vec3(vs, tc);
    float l = fbm6(vc);
    l = smoothstep(0.0, 1.0, l);
    l = max(0.0, (l - pow(q.y * 0.8, 0.6)) * 1.8);
    //float r = pow(l , 1.5);
    //float g = pow(l , 3.0);
    //float b = pow(l , 6.0);
	
	// l += gradient(q.y, 0.0, 1.0, 0.8);
    // l = smoothstep(0.0, 1.0, l);
	
	return vec4(l, l, l, 1.0);
    //return vec4( r, g, b, 1.0 );
}

//*****************************************************************************



//*****************************************************************************

vec4 create_colors(vec2 input_coords)
{
    vec4 top_color = vec4(0.0);
    vec4 bottom_color = vec4(0.0);
    
    vec2 draw_area = vec2(flame_width, flame_height);
    //vec2 screen_area = iResolution.xy;
    vec2 screen_area = object_size.xy;

    if(is_reflected)
    {
        vec2 top_coords = vec2(input_coords.x, screen_area.y - input_coords.y);

        top_color = create_color(top_coords, draw_area, screen_area);
    }
    
    bottom_color = create_color(input_coords, draw_area, screen_area);
    
    bool mirror_value = is_mirror || !is_reflected;
	
	//bottom_color += gradient(input_coords.y, 0.0, 0.1, 0.01);
	//top_color += gradient(screen_area.y - input_coords.y, 0.0, 2.0, 1.0);

    return bottom_color * float(mirror_value) + top_color;
}

//*****************************************************************************

float normpdf(in float x, in float sigma)
{
	return 0.39894 * exp(-0.5 * x * x / (sigma * sigma)) / sigma;
}

//*****************************************************************************



//*****************************************************************************

vec4 render(vec2 input_coords)
{
    vec3 c = create_colors(input_coords).rgb;

    // Declare stuff
    const int mSize = 5; // Adjusted size of the kernel
    const int kSize = (mSize - 1) / 2;
    float kernel[mSize];
    vec3 finalColor = vec3(0.0);

    // Create the 1-D kernel
    float Z = 0.0;
    for (int j = 0; j <= kSize; ++j)
    {
        kernel[kSize + j] = kernel[kSize - j] = normpdf(float(j), sigma);
    }

    // Get the normalization factor (as the gaussian has been clamped)
    for (int j = 0; j < mSize; ++j)
    {
        Z += kernel[j];
    }
	
	//read out the texels
    for (int i=-kSize; i <= kSize; ++i)
    {
        for (int j=-kSize; j <= kSize; ++j)
        {
            finalColor += kernel[kSize+j] * kernel[kSize+i] * c.rgb;
        }
    }
	
    finalColor /= Z * Z;

    finalColor = c + pow(finalColor, vec3(0.5)) * 0.5;
	
	float alpha = clamp(alpha_percentage * 0.01, 0.0, 1.0);
	
	vec4 col = vec4(finalColor.r, finalColor.g, finalColor.b, alpha);
	
	if (is_apply_to_alpha_layer)
    {    
        vec4 background_color = vec4(0.0, 0.0, 0.0, 0.0);

		col = apply_to_alpha_layer(background_color, col, alpha);
    }

    vec4 multi_color = clamp(vec4(flame_multi_color, alpha), 0.0, 1.0);
	col *= multi_color;

	return col;
}

//*****************************************************************************



//*****************************************************************************

vec4 apply_to_alpha_layer(vec4 background_color, vec4 input_color, float alpha)
{
    vec4 original_color = background_color;
 
    float luma = dot(input_color.rgb, vec3(0.299, 0.587, 0.114));
    float luma_sum = luma_min + luma_min_smooth;
    float luma_min_res = smoothstep(luma_min, luma_sum, luma);
    input_color.a = clamp(luma_min_res, 0.0, 1.0);

    if (background_color.a == 0.0)
    {
        return input_color;
    }
    
    input_color.rgb = mix(original_color.rgb, input_color.rgb, alpha);
    input_color.a = mix(original_color.a, input_color.a, input_color.a);
    //input_color = mix(original_color, input_color, input_color.a);

    return input_color;
}

//*****************************************************************************







float gradient(float value, float min, float max, float height)
{
    return mix(max, min, value + height);
}

`;























// vec4 render(vec2 input_coords)
// {
//     vec3 c = create_colors(input_coords).rgb;

//     // Declare stuff
//     const int mSize = 5; // Adjusted size of the kernel
//     const int kSize = (mSize - 1) / 2;
//     float kernel[mSize];
//     vec3 finalColor = vec3(0.0);

//     // Create the 1-D kernel
//     float Z = 0.0;
//     for (int j = 0; j <= kSize; ++j)
//     {
//         kernel[kSize + j] = kernel[kSize - j] = normpdf(float(j), sigma);
//     }

//     // Get the normalization factor (as the gaussian has been clamped)
//     for (int j = 0; j < mSize; ++j)
//     {
//         Z += kernel[j];
//     }
	
// 	//read out the texels
//     for (int i=-kSize; i <= kSize; ++i)
//     {
//         for (int j=-kSize; j <= kSize; ++j)
//         {
//             finalColor += kernel[kSize+j] * kernel[kSize+i] * c.rgb;
//         }
//     }
	
//     finalColor /= Z * Z;

//     finalColor = c + pow(finalColor, vec3(0.5)) * 0.5;
	
// 	float alpha = clamp(alpha_percentage * 0.01, 0.0, 1.0);
	
// 	vec4 col = vec4(finalColor.r, finalColor.g, finalColor.b, alpha);
	
// 	if (is_apply_to_alpha_layer)
//     {    
//         vec4 background_color = vec4(0.0, 0.0, 0.0, 0.0);

// 		col = apply_to_alpha_layer(background_color, col, alpha);
//     }

//     vec4 multi_color = clamp(vec4(flame_multi_color, 1.0), 0.0, 1.0);
// 	col *= multi_color;

// 	return col;
// }