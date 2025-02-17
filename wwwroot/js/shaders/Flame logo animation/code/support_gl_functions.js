function init_WebGL(canvas) 
{
    const gl = canvas.getContext('webgl2');
    if (!gl) 
    {
        alert('Unable to initialize WebGL2. Your browser may not support it.');
        return null;
    }

    return gl;
}

//*****************************************************************************

function create_shader(gl, source_code, type) 
{
    const shader = gl.createShader(type);
    gl.shaderSource(shader, source_code);
    gl.compileShader(shader);

    if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) 
    {
        console.error('An error occurred compiling the shaders: ' + 
			gl.getShaderInfoLog(shader));
        gl.deleteShader(shader);

        return null;
    }

    return shader;
}

//*****************************************************************************

function create_program(gl, vertex_shader, fragment_shader) 
{
    const shader_program = gl.createProgram();
    gl.attachShader(shader_program, vertex_shader);
    gl.attachShader(shader_program, fragment_shader);
    gl.linkProgram(shader_program);
    if (!gl.getProgramParameter(shader_program, gl.LINK_STATUS)) 
    {
        console.error('Unable to initialize the shader program: ' + 
			gl.getProgramInfoLog(shaderProgram));

        return null;
    }

    return shader_program;
}

//*****************************************************************************

function normalize(value, min, max) 
{
    return (value - min) / (max - min);
}

//*****************************************************************************

function get_uniform_location(gl, shader, name)
{
    return gl.getUniformLocation(shader, name);
}

//*****************************************************************************

function to_radians(angle)
{
	return angle * Math.PI / 180.0;
}

//*****************************************************************************

function create_ring(center, inner_radius, outer_radius, segments_count = 360, 
    start_angle = 0, end_angle = Math.PI * 2) 
{
    const TAU = Math.PI * 2;
    const angle = Math.min(TAU, end_angle - start_angle);

    const positions = angle === TAU ? [] : [center[0], center[1]];
    const tex_coords = angle === TAU ? [] : [center[0], center[1]];

    const segments = Math.round(segments_count * angle / TAU);

    const x0 = center[0];
    const y0 = center[1];

    for(let i = 0; i <= segments; i++) 
    {
        const i_angle = start_angle + angle * i / segments;

        const cos_angle = Math.cos(i_angle);
        const sin_angle = Math.sin(i_angle);

        const x1 = x0 + inner_radius * cos_angle;
        const y1 = y0 + inner_radius * sin_angle;
        const x2 = x0 + outer_radius * cos_angle;
        const y2 = y0 + outer_radius * sin_angle;

        positions.push(x1, y1, x2, y2);

        // Нормализуем угол в диапазон от 0 до 1
        const norm_angle = (i_angle - start_angle) / angle;

        const tx_1 = norm_angle;
        const ty_1 = 1.0;
        const tx_2 = norm_angle;
        const ty_2 = 0.0;

        tex_coords.push(tx_1, ty_1, tx_2, ty_2);
    }

    let result = new Ring(inner_radius, outer_radius, segments_count, 
        start_angle, end_angle);
    result.positions = new Float32Array(positions);
    result.tex_coords = new Float32Array(tex_coords);
    result.vertices_count = positions.length;

    return result;
}

//*****************************************************************************

function create_framebuffer(gl, texture) 
{
    const framebuffer = gl.createFramebuffer();
    gl.bindFramebuffer(gl.FRAMEBUFFER, framebuffer);
    gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, 
        gl.TEXTURE_2D, texture, 0);

    return framebuffer;
}

//*****************************************************************************

function create_texture(gl, width, height) 
{
    const texture = gl.createTexture();
    gl.bindTexture(gl.TEXTURE_2D, texture);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, width, height, 0, gl.RGBA, 
        gl.UNSIGNED_BYTE, null);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
	gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);

    return texture;
}

//*****************************************************************************

function create_arc(center, radius, segments_count, start_angle = 0, 
    end_angle = Math.PI * 2)
{
    const TAU = Math.PI * 2;
    const angle = Math.min(TAU, end_angle - start_angle);

    const positions = [];
    const tex_coords = [];

    if(angle == TAU)
    {
        positions.push(center[0], center[1]);
        tex_coords.push(center[0], center[1]);
    }

    const segments = Math.round(segments_count * angle / TAU);

    let x0 = center[0];
    let y0 = center[1];

    for(let i = 0; i <= segments; i++) 
    {
        const i_angle = start_angle + angle * i / segments;

        const cos_angle = Math.cos(i_angle);
        const sin_angle = Math.sin(i_angle);

        const x1 = x0 + radius * cos_angle;
        const y1 = y0 + radius * sin_angle;

        positions.push(x1, y1);

        // Нормализуем угол в диапазон от 0 до 1
        const norm_angle = (i_angle - start_angle) / angle;

        const tx_1 = norm_angle;
        const ty_1 = 1.0;

        tex_coords.push(tx_1, ty_1);
    }

    return { 'positions': positions, 'tex_coords': tex_coords };
}

//*****************************************************************************

function create_circle(radius, segments_count = 60, start_angle = 0,
    end_angle = 360)
{
    const TAU = Math.PI * 2;
    const angle = Math.min(TAU, end_angle - start_angle);

    const positions = [];
    const tex_coords = [];

    let center = [0.0, 0.0];

    if (angle === TAU) {
        positions.push(center[0], center[1]);
        tex_coords.push(0.5, 0.5);
    }

    const segments = Math.round(segments_count * angle / TAU);

    let x0 = center[0];
    let y0 = center[1];

    for (let i = 0; i <= segments; i++) {
        const i_angle = start_angle + angle * i / segments;

        const cos_angle = Math.cos(i_angle);
        const sin_angle = Math.sin(i_angle);

        const x1 = x0 + radius * cos_angle;
        const y1 = y0 + radius * sin_angle;

        positions.push(x1, y1);

        const u = 0.5 + 0.5 * cos_angle;
        const v = 0.5 - 0.5 * sin_angle;

        tex_coords.push(u, v);
    }

    let result = new Circle(radius);
    result.positions = new Float32Array(positions);
    result.tex_coords = new Float32Array(tex_coords);
    result.vertices_count = positions.length;

    return result;
}

//*****************************************************************************

function create_orthographic_matrix(left, right, bottom, top, near, far) 
{
    let projection_matrix = mat4.create();
	
    mat4.ortho(projection_matrix, left, right, bottom, top, near, far);
	
    return projection_matrix;
}

//*****************************************************************************

function check_shaders(...shaders) 
{
    if (shaders.length % 2 !== 0) 
    {
        console.error("Incorrect number of parameters.", 
            "Each shader must be accompanied by error text");
        return false;
    }
  
    // Обрабатываем параметры парами
    for (let i = 0; i < shaders.length; i += 2) 
    {
        const shader = shaders[i];
        const shader_error_text = shaders[i + 1];

        // Выполняем проверку шейдера
        if (!shader) 
        {
            console.error('Error in ', shader_error_text);

            return false;
        }
    }

    return true;
}

//*****************************************************************************

function to_seconds(time)
{
    return time * 0.001;
}

//*****************************************************************************

function to_millisecond(time)
{
    return time * 1000;
}

//*****************************************************************************