
function setup_flame_shader(gl, shader, projection_matrix, noise_texture_size, 
    settings)
{
    gl.useProgram(shader);

    const time_effect_loc = get_uniform_location(gl, shader, 'time_effect');
    const flame_height_loc = get_uniform_location(gl, shader, 'flame_height');
    const sigma_loc = get_uniform_location(gl, shader, 'sigma');
    const luma_min_loc = get_uniform_location(gl, shader, 'luma_min');
    const luma_min_smooth_loc = get_uniform_location(gl, shader, 
        'luma_min_smooth');
    const is_mirror_loc = get_uniform_location(gl, shader, 'is_mirror');
    const is_reflected_loc = get_uniform_location(gl, shader, 'is_reflected');
    const is_apply_to_alpha_layer_loc = get_uniform_location(gl, shader, 
        'is_apply_to_alpha_layer');
    const alpha_percentage_loc = get_uniform_location(gl, shader, 
        'alpha_percentage');
    const flame_multi_color_loc = get_uniform_location(gl, shader, 
        'flame_multi_color');
    const projection_matrix_loc = get_uniform_location(gl, shader, 
        'projection_matrix');
    const noise_texture_loc = get_uniform_location(gl, shader, 
        'noise_texture');
    const noise_texture_size_loc = get_uniform_location(gl, shader,
        'noise_texture_size');
    
    gl.uniform1f(time_effect_loc, settings.time_effect);
    gl.uniform1f(flame_height_loc, settings.flame_height);
    gl.uniform1f(sigma_loc, settings.sigma);
    gl.uniform1f(luma_min_loc, settings.luma_min);
    gl.uniform1f(luma_min_smooth_loc, settings.luma_min_smooth);
    gl.uniform1i(is_mirror_loc, settings.is_mirror);
    gl.uniform1i(is_reflected_loc, settings.is_reflected);
    gl.uniform1i(is_apply_to_alpha_layer_loc, 
        settings.is_apply_to_alpha_layer);
    gl.uniform1f(alpha_percentage_loc, settings.alpha_percentage);	
    let flame_multi_color = settings.flame_multi_color;
    gl.uniform3f(flame_multi_color_loc, flame_multi_color.r / 255, 
        flame_multi_color.g / 255, flame_multi_color.b / 255);
    gl.uniformMatrix4fv(projection_matrix_loc, false, projection_matrix);
    gl.uniform1i(noise_texture_loc, 0);
    gl.uniform2f(noise_texture_size_loc, noise_texture_size[0], 
        noise_texture_size[1]);
}

//*****************************************************************************

function setup_logo_shader(gl, shader, projection_matrix)
{
    gl.useProgram(shader);

    const logo_texture_loc = get_uniform_location(gl, shader, 
        'logo_texture');
    const projection_matrix_loc = get_uniform_location(gl, shader, 
        'projection_matrix');

    gl.uniform1i(logo_texture_loc, 0);
    gl.uniformMatrix4fv(projection_matrix_loc, false, projection_matrix);
}


//*****************************************************************************



//*****************************************************************************
//                                  MAIN
//*****************************************************************************

function main() 
{
    const canvas = document.getElementById('glCanvas');
    canvas.width = window.innerWidth;
    canvas.height = window.innerHeight;

    const gl = init_WebGL(canvas);
    if (!gl) return;
	
    const flame_vertex_shader = create_shader(gl, vertex_shader_source, 
        gl.VERTEX_SHADER);
    const flame_fragment_shader = create_shader(gl, fragment_shader_source,
        gl.FRAGMENT_SHADER);
    
    const logo_vertex_shader = create_shader(gl, vertex_shader_source_2,
        gl.VERTEX_SHADER);
    const logo_fragment_shader = create_shader(gl, fragment_shader_source_2,
        gl.FRAGMENT_SHADER);

    let shaders_result = check_shaders(
        flame_vertex_shader, 'flame vertex shader creation!',
        flame_fragment_shader, 'flame fragment shader creation!',
        logo_vertex_shader, 'logo vertex shader creation!',
        logo_fragment_shader, 'logo fragment shader creation!');

    if(!shaders_result) return;

    const flame_shader = create_program(gl, flame_vertex_shader,
        flame_fragment_shader);

    const logo_shader = create_program(gl, logo_vertex_shader,
        logo_fragment_shader);

    shaders_result = check_shaders(
        flame_shader, 'flame shader program creation!',
        logo_shader, 'logo shader program creation!');

    if(!shaders_result) return;

    gl.deleteShader(flame_vertex_shader);
    gl.deleteShader(flame_fragment_shader);
    gl.deleteShader(logo_vertex_shader);
    gl.deleteShader(logo_fragment_shader);

    let main_status = false;
    let resolution_size = [gl.canvas.width, gl.canvas.height];

    let near_far = [-10, 10];
    let left = 0;
    let right = resolution_size[0];
    let top = resolution_size[1];
    let bottom = 0;
    
    let projection_matrix = create_orthographic_matrix(left, right, 
        bottom, top, near_far[0], near_far[1]);

    let noise_texture_size = [0, 0];
    let noise_texture = null;

    let logo_texture_size = [0.0, 0.0];
    let logo_texture = null;

    const noise_img = new Image();
    const logo_img = new Image();

    noise_img.onload = function() 
    {
        console.log('Noise texture loaded successfully.');

        noise_texture = gl.createTexture();
        gl.bindTexture(gl.TEXTURE_2D, noise_texture);
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, 
            gl.UNSIGNED_BYTE, noise_img);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);

        noise_texture_size = [noise_img.width, noise_img.height];

        setup_flame_shader(gl, flame_shader, projection_matrix, 
            noise_texture_size, shader_settings);

        main_status = noise_texture != null && logo_texture != null;
	}

    noise_img.src = fractal_brownian_motion_noise_texture_path;

    logo_img.onload = function() 
    {
        console.log('Logo texture loaded successfully.');

        logo_texture = gl.createTexture();
        gl.bindTexture(gl.TEXTURE_2D, logo_texture);
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, 
            gl.UNSIGNED_BYTE, logo_img);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
        
        logo_texture_size = [logo_img.width, logo_img.height];

        setup_logo_shader(gl, logo_shader, projection_matrix);

        main_status = noise_texture != null && logo_texture != null;
    }

    logo_img.src = "http://localhost:9255/js/shaders/logo/yZg2gMSD36w.jpg";

    logo_circle = create_circle(shader_settings.logo_radius);
    logo_circle.init_buffers(gl, logo_shader);
    logo_circle.model.set_position(
        resolution_size[0] / 2.0,
        resolution_size[1] / 2.0);

    let inner_radius = shader_settings.logo_radius - 1.0;
    let outer_radius = inner_radius + shader_settings.logo_flame_height;

    logo_flame_border = create_ring([0.0, 0.0], inner_radius, outer_radius);
    logo_flame_border.init_buffers(gl, flame_shader);
    logo_flame_border.model.set_position(
        resolution_size[0] / 2.0,
        resolution_size[1] / 2.0);

    let logo_frame_border_width = 2.0 * Math.PI * outer_radius;
    let logo_frame_border_height = outer_radius - inner_radius;

    logo_radius_loc = get_uniform_location(gl, logo_shader, 'radius');



    const iResolution_loc = get_uniform_location(gl, flame_shader, 
        'iResolution');
    const iTime_loc = get_uniform_location(gl, flame_shader, 'iTime');

    const flame_width_loc = get_uniform_location(gl, flame_shader, 
        'flame_width');

    const flame_height_loc = get_uniform_location(gl, flame_shader, 
        'flame_height');

    const object_size_loc = get_uniform_location(gl, flame_shader,
        'object_size');



    let last_time = 0.0;
    let dt = 0.0;
    let interval_fps = 1.0 / shader_settings.max_fps;
    let need_render = false;

    let logo_rotation_speed = shader_settings.logo_rotation_speed;
    let logo_scale_speed = shader_settings.logo_scale_speed;
    let logo_border_rotation_speed = 
        shader_settings.logo_border_rotation_speed;
    let logo_border_scale_speed = shader_settings.logo_border_scale_speed;

    function render(time) 
    {
        requestAnimationFrame(render);

        let tmp_time = to_seconds(time);

        dt = tmp_time - last_time;

        if(!main_status)
        {
            last_time = tmp_time;
        }

        if(dt >= interval_fps)
        {
            last_time = tmp_time - (dt % interval_fps);

            need_render = true;
        }

        if(!need_render) return;

        gl.viewport(0, 0, gl.canvas.width, gl.canvas.height);
        gl.clearColor(0.0, 0.0, 0.0, 0.0);

        gl.clear(gl.COLOR_BUFFER_BIT);

        gl.useProgram(flame_shader);
        gl.uniform1f(iTime_loc, tmp_time);
        gl.uniform2f(object_size_loc, logo_frame_border_width, 
            logo_frame_border_height);
        gl.uniform1f(flame_width_loc, logo_frame_border_width);
        gl.uniform1f(flame_height_loc, logo_frame_border_height);

        gl.activeTexture(gl.TEXTURE0);
        gl.bindTexture(gl.TEXTURE_2D, noise_texture);

        logo_flame_border.model.rotate(dt * logo_border_rotation_speed);
        logo_flame_border.model.scale(dt * logo_border_scale_speed, 
            dt * logo_border_scale_speed);
        logo_flame_border.draw(gl, flame_shader);

        gl.useProgram(logo_shader);
        gl.activeTexture(gl.TEXTURE0);
        gl.bindTexture(gl.TEXTURE_2D, logo_texture);

        logo_circle.model.rotate(dt * logo_rotation_speed);
        logo_circle.model.scale(dt * logo_scale_speed, dt * logo_scale_speed);
        logo_circle.draw(gl, logo_shader);

        console.log('REnder');

        need_render = false;
    }

    requestAnimationFrame(render);
}

//*****************************************************************************

main();