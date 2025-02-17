
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

function setup_texture_shader(gl, shader, projection_matrix, resolution_size,
     textures_size)
{
    gl.useProgram(shader);

    const iResolution_loc = get_uniform_location(gl, shader, 'iResolution');
    const texture_size_loc = get_uniform_location(gl, shader, 'texture_size');
    const border_texture_loc = get_uniform_location(gl, shader, 
        'border_texture');
    const corner_texture_loc = get_uniform_location(gl, shader, 
        'corner_texture');
    const projection_matrix_loc = get_uniform_location(gl, shader, 
        'projection_matrix');

    gl.uniform2f(iResolution_loc, resolution_size[0], resolution_size[1]);
    gl.uniform2f(texture_size_loc, textures_size[0], textures_size[1]);
    gl.uniform1i(border_texture_loc, 0); 
    gl.uniform1i(corner_texture_loc, 1);
    gl.uniformMatrix4fv(projection_matrix_loc, false, projection_matrix);
}

//*****************************************************************************

function setup_logo_shader(gl, shader, projection_matrix, resolution_size,
    texture_size)
{
    gl.useProgram(shader);

    const iResolution_loc = get_uniform_location(gl, shader, 'iResolution');
    const texture_size_loc = get_uniform_location(gl, shader, 'texture_size');
    const logo_texture_loc = get_uniform_location(gl, shader, 
        'logo_texture');
    const projection_matrix_loc = get_uniform_location(gl, shader, 
        'projection_matrix');

    gl.uniform2f(iResolution_loc, resolution_size[0], resolution_size[1]);
    gl.uniform2f(texture_size_loc, texture_size[0], texture_size[1]);
    gl.uniform1i(logo_texture_loc, 0);
    gl.uniformMatrix4fv(projection_matrix_loc, false, projection_matrix);
}

//*****************************************************************************

function update_fade_animation(delta_time, settings, fade_data)
{
    if(settings.is_unfading)
    {
        fade_data.unfade_time += delta_time;

        if(fade_data.unfade_time <= shader_settings.unfading_duration)
        {
            fade_data.long_flame_height += delta_time * 
                fade_data.long_unfade_effect;
            fade_data.short_flame_height += delta_time * 
                fade_data.short_unfade_effect;
            fade_data.corner_flame_height += delta_time * 
                fade_data.corner_unfade_effect;
        }
        else
        {
            console.log('END UNFADE!');
            settings.is_unfading = false;
            fade_data.unfade_time = 0.0;

            fade_data.long_flame_height = fade_data.end_long_flame_height;
            fade_data.short_flame_height = fade_data.end_short_flame_height;
            fade_data.corner_flame_height = fade_data.end_corner_flame_height;
        }
    }
    else if (settings.is_fading)
    {
        fade_data.fade_time += delta_time;

        if(fade_data.fade_time <= shader_settings.fading_duration)
        {
            fade_data.long_flame_height -= delta_time * 
                fade_data.long_fade_effect;
            fade_data.short_flame_height -= delta_time * 
                fade_data.short_fade_effect;
            fade_data.corner_flame_height -= delta_time * 
                fade_data.corner_fade_effect;
        }
        else
        {
            console.log('END FADE!');
            settings.is_fading = false;

            fade_data.fade_time = 0.0;
            
            fade_data.long_flame_height = fade_data.begin_long_flame_height;
            fade_data.short_flame_height = fade_data.begin_short_flame_height;
            fade_data.corner_flame_height = 
                fade_data.begin_corner_flame_height;
        }
    }
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
    
    const texture_vertex_shader = create_shader(gl, vertex_shader_source_2,
        gl.VERTEX_SHADER);
    const texture_fragment_shader = create_shader(gl, fragment_shader_source_2,
        gl.FRAGMENT_SHADER);

    if(shader_settings.is_logo_animation)
    {
        logo_vertex_shader = create_shader(gl, vertex_shader_source_3,
            gl.VERTEX_SHADER);
        logo_fragment_shader = create_shader(gl, fragment_shader_source_3,
            gl.FRAGMENT_SHADER);
    }

    let shaders_result = check_shaders(
        flame_vertex_shader, 'flame vertex shader creation!',
        flame_fragment_shader, 'flame fragment shader creation!',
        texture_vertex_shader, 'texture vertex shader creation!',
        texture_fragment_shader, 'texture fragment shader creation!');

    if(!shaders_result) return;

    const flame_shader = create_program(gl, flame_vertex_shader,
        flame_fragment_shader);

    const texture_shader = create_program(gl, texture_vertex_shader,
        texture_fragment_shader);

    shaders_result = check_shaders(
        flame_shader, 'flame shader program creation!',
        texture_shader, 'texture shader program creation!');

    if(!shaders_result) return;

    gl.deleteShader(flame_vertex_shader);
    gl.deleteShader(flame_fragment_shader);
    gl.deleteShader(texture_vertex_shader);
    gl.deleteShader(texture_fragment_shader);

    let main_status = false;
    let resolution_size = [gl.canvas.width, gl.canvas.height];
    let screen_texture_size = resolution_size;

    let near_far = [-10, 10];
    let left = 0;
    let right = resolution_size[0];
    let top = resolution_size[1];
    let bottom = 0;
    
    let projection_matrix = create_orthographic_matrix(left, right, 
        bottom, top, near_far[0], near_far[1]);

    let noise_texture_size = [0, 0];
    let noise_texture = null;

    const img = new Image();

    img.onload = function() 
    {
        console.log('Noise texture loaded successfully.');

        noise_texture = gl.createTexture();
        gl.bindTexture(gl.TEXTURE_2D, noise_texture);
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, 
            gl.UNSIGNED_BYTE, img);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);

        noise_texture_size = [img.width, img.height];

        setup_flame_shader(gl, flame_shader, projection_matrix, 
            noise_texture_size, shader_settings);
        setup_texture_shader(gl, texture_shader, projection_matrix, 
            resolution_size, screen_texture_size);

        main_status = true;
	}

    img.src = fractal_brownian_motion_noise_texture_path;



    let screen_sprite_width = resolution_size[0];
    let screen_sprite_height = resolution_size[1];
    let screen_sprite = create_sprite(screen_sprite_width,
        screen_sprite_height);
    screen_sprite.init_buffers(gl, texture_shader);
    
    let corner_sprite_size = (shader_settings.flame_height) * 2.0;
    let corner_sprite_radius = shader_settings.corner_radius === 0 ? 0.01 : shader_settings.corner_radius;
    let corner_sprite_segments = 120;
    let corner_sprite_begin_angle = to_radians(270);
    let corner_sprite_end_angle = to_radians(180);

    let corner_sprite = create_rectangle_with_hole(corner_sprite_size, 
        corner_sprite_size, corner_sprite_radius, corner_sprite_segments, 
        corner_sprite_begin_angle, corner_sprite_end_angle);
    corner_sprite.init_buffers(gl, flame_shader);

    let border_sprite_height = shader_settings.flame_height * 1.0;
    let border_sprite_long_width = resolution_size[0] - 
        2 * border_sprite_height;
    let border_sprite_short_width = resolution_size[1] - 
        2 * border_sprite_height;

    let border_long_sprite = create_sprite(border_sprite_long_width, 
        border_sprite_height);
    border_long_sprite.init_buffers(gl, flame_shader);

    let border_short_sprite = create_sprite(border_sprite_short_width, 
        border_sprite_height);
    border_short_sprite.init_buffers(gl, flame_shader);

    const border_texture = create_texture(gl, screen_texture_size[0], 
        screen_texture_size[1]);
    const border_framebuffer = create_framebuffer(gl, border_texture);

    const corner_texture = create_texture(gl, screen_texture_size[0], 
        screen_texture_size[1]);
    const corner_framebuffer = create_framebuffer(gl, corner_texture);

    const flame_iResolution_loc = get_uniform_location(gl, flame_shader, 
        'iResolution');
    const flame_iTime_loc = get_uniform_location(gl, flame_shader, 'iTime');

    const flame_width_loc = get_uniform_location(gl, flame_shader, 'flame_width');

    const flame_height_loc = get_uniform_location(gl, flame_shader, 
        'flame_height');

    const object_size_loc = get_uniform_location(gl, flame_shader,
        'object_size');

    const is_reflected_loc = get_uniform_location(gl, flame_shader,
        'is_reflected');



    g_fade_data.end_long_flame_height = shader_settings.flame_height;
    g_fade_data.end_short_flame_height = shader_settings.flame_height;
    g_fade_data.end_corner_flame_height = shader_settings.flame_height;

    g_fade_data.long_unfade_effect = g_fade_data.end_long_flame_height / 
        shader_settings.unfading_duration;
    g_fade_data.short_unfade_effect = g_fade_data.end_short_flame_height / 
        shader_settings.unfading_duration;
    g_fade_data.corner_unfade_effect = g_fade_data.end_corner_flame_height / 
        shader_settings.unfading_duration;

    g_fade_data.long_fade_effect = g_fade_data.end_long_flame_height / 
        shader_settings.fading_duration;
    g_fade_data.short_fade_effect = g_fade_data.end_short_flame_height / 
        shader_settings.fading_duration;
    g_fade_data.corner_fade_effect = g_fade_data.end_corner_flame_height / 
        shader_settings.fading_duration;

    if(!shader_settings.is_unfading)
    {
        g_fade_data.long_flame_height = g_fade_data.end_long_flame_height;
        g_fade_data.short_flame_height = g_fade_data.end_short_flame_height;
        g_fade_data.corner_flame_height = g_fade_data.end_corner_flame_height;
    }



    let last_time = 0.0;
    let dt = 0.0;
    let interval_fps = 1.0 / shader_settings.max_fps;
    let need_render = false;



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

        update_fade_animation(dt, shader_settings, g_fade_data);

        if(     !need_render
            ||  g_fade_data.long_flame_height == 0.0
            ||  g_fade_data.short_fade_height == 0.0
            ||  g_fade_data.corner_fade_height == 0.0)
        {
            return;
        }

        gl.viewport(0, 0, gl.canvas.width, gl.canvas.height);
        gl.clearColor(0.0, 0.0, 0.0, 0.0); // Устанавливаем черный фон

        gl.useProgram(flame_shader);
        gl.uniform1f(flame_iTime_loc, tmp_time);
        gl.uniform1i(is_reflected_loc, shader_settings.is_reflected);
        gl.bindFramebuffer(gl.FRAMEBUFFER, border_framebuffer);

        gl.clear(gl.COLOR_BUFFER_BIT);

        gl.activeTexture(gl.TEXTURE0);
        gl.bindTexture(gl.TEXTURE_2D, noise_texture);

        gl.uniform2f(flame_iResolution_loc, resolution_size[0], 
            resolution_size[1]);
        gl.uniform2f(object_size_loc, border_sprite_long_width, 
            border_sprite_height);
        gl.uniform1f(flame_width_loc, border_sprite_long_width);
        gl.uniform1f(flame_height_loc, g_fade_data.long_flame_height);

        border_long_sprite.model.set_position(border_sprite_height, 0.0);
        border_long_sprite.model.set_rotation(0);
        border_long_sprite.draw(gl, flame_shader);

        border_long_sprite.model.set_position(resolution_size[0] - 
            border_sprite_height, resolution_size[1]);
        border_long_sprite.model.set_rotation(180);
        border_long_sprite.draw(gl, flame_shader);

        gl.uniform2f(flame_iResolution_loc, resolution_size[0], 
            resolution_size[1]);
        gl.uniform2f(object_size_loc, border_sprite_short_width, 
            border_sprite_height);
        gl.uniform1f(flame_width_loc, border_sprite_short_width);
        gl.uniform1f(flame_height_loc, g_fade_data.short_flame_height);

        border_short_sprite.model.set_position(0.0, 
            resolution_size[1] - border_sprite_height);
        border_short_sprite.model.set_rotation(-90);
        border_short_sprite.draw(gl, flame_shader);

        border_short_sprite.model.set_position(resolution_size[0], 
            border_sprite_height);
        border_short_sprite.model.set_rotation(90);
        border_short_sprite.draw(gl, flame_shader);



        gl.bindFramebuffer(gl.FRAMEBUFFER, corner_framebuffer);

        gl.clear(gl.COLOR_BUFFER_BIT);

        gl.activeTexture(gl.TEXTURE0);
        gl.bindTexture(gl.TEXTURE_2D, noise_texture);

        gl.uniform2f(flame_iResolution_loc, resolution_size[0], 
            resolution_size[1]);
        gl.uniform2f(object_size_loc, corner_sprite_size, 
            corner_sprite_size);
        gl.uniform1f(flame_width_loc, corner_sprite_size * 0.5);
        gl.uniform1f(flame_height_loc, g_fade_data.corner_flame_height * 2.0);

        corner_sprite.model.set_position(0.0, 0.0);
        corner_sprite.model.set_rotation(0);
        corner_sprite.draw(gl, flame_shader);

        corner_sprite.model.set_position(0.0, resolution_size[1]);
        corner_sprite.model.set_rotation(-90);
        corner_sprite.draw(gl, flame_shader);

        corner_sprite.model.set_position(resolution_size[0], 
            resolution_size[1]);
        corner_sprite.model.set_rotation(-180);
        corner_sprite.draw(gl, flame_shader);

        corner_sprite.model.set_position(resolution_size[0], 0.0);
        corner_sprite.model.set_rotation(90);
        corner_sprite.draw(gl, flame_shader);



        gl.useProgram(texture_shader);
        gl.bindFramebuffer(gl.FRAMEBUFFER, null);

        gl.clear(gl.COLOR_BUFFER_BIT);

        gl.activeTexture(gl.TEXTURE0);
        gl.bindTexture(gl.TEXTURE_2D, border_texture);

        gl.activeTexture(gl.TEXTURE1);
        gl.bindTexture(gl.TEXTURE_2D, corner_texture);

        screen_sprite.draw(gl, texture_shader);

        console.log('REnder');

        need_render = false;
    }

    requestAnimationFrame(render);
}

//*****************************************************************************

main();



























// function render(time) 
//     {
//         let tmp_time = to_seconds(time);

//         dt = tmp_time - last_time;
//         last_time = tmp_time;

//         //console.log('DT: ', dt);

//         if(!main_status) requestAnimationFrame(render);

//         if(dt < interval_fps)
//         {
//             //requestAnimationFrame(render);

//             if(shader_settings.is_fading)
//                 {
//                     if(tmp_time < shader_settings.fading_duration)
//                     {
//                         long_flame_height -= dt * long_flame_effect;
//                         short_flame_height -= dt * short_flame_effect;
//                         corner_flame_height -= dt * corner_flame_effect;
//                         //alpha -= dt * alha_flame_effect;
        
//                         console.log('STEP!');
//                     }
//                     else
//                     {
//                         long_flame_height = 0.0;
//                         short_flame_height = 0.0;
//                         corner_flame_height = 0.0;
//                         //alpha = 0.0;
        
//                         console.log('STOP!');
//                         //shader_settings.is_fading = false;
//                         //main_status = false;
        
//                         return;
//                     }
//                 }

//             setTimeout(() => requestAnimationFrame(render), interval_fps * 1000 - (time - last_time * 1000));
//             return; // Если нет, выходим из функции
//         }

//         if(shader_settings.is_fading)
//         {
//             if(tmp_time < shader_settings.fading_duration)
//             {
//                 long_flame_height -= dt * long_flame_effect;
//                 short_flame_height -= dt * short_flame_effect;
//                 corner_flame_height -= dt * corner_flame_effect;
//                 //alpha -= dt * alha_flame_effect;

//                 console.log('STEP!');
//             }
//             else
//             {
//                 long_flame_height = 0.0;
//                 short_flame_height = 0.0;
//                 corner_flame_height = 0.0;
//                 //alpha = 0.0;

//                 console.log('STOP!');
//                 //shader_settings.is_fading = false;
//                 //main_status = false;

//                 return;
//             }
//         }

//         gl.viewport(0, 0, gl.canvas.width, gl.canvas.height);
//         gl.clearColor(0.0, 0.0, 0.0, 0.0); // Устанавливаем черный фон



//         gl.useProgram(flame_shader);
//         gl.uniform1f(flame_iTime_loc, tmp_time);
//         //gl.uniform1f(alpha_percentage_loc, alpha);	
//         gl.bindFramebuffer(gl.FRAMEBUFFER, border_framebuffer);

//         gl.clear(gl.COLOR_BUFFER_BIT);

//         gl.activeTexture(gl.TEXTURE0);
//         gl.bindTexture(gl.TEXTURE_2D, noise_texture);

//         gl.uniform2f(flame_iResolution_loc, resolution_size[0], 
//             resolution_size[1]);
//         gl.uniform2f(object_size_loc, border_sprite_long_width, border_sprite_height);
//         gl.uniform1f(flame_width_loc, border_sprite_long_width);
//         gl.uniform1f(flame_height_loc, long_flame_height);

//         border_long_sprite.model.set_position(border_sprite_height, 0.0);
//         border_long_sprite.model.set_rotation(0);
//         border_long_sprite.draw(gl, flame_shader);

//         border_long_sprite.model.set_position(resolution_size[0] - 
//             border_sprite_height, resolution_size[1]);
//         border_long_sprite.model.set_rotation(180);
//         border_long_sprite.draw(gl, flame_shader);

//         gl.uniform2f(flame_iResolution_loc, resolution_size[0], 
//             resolution_size[1]);
//         gl.uniform2f(object_size_loc, border_sprite_short_width, border_sprite_height);
//         gl.uniform1f(flame_width_loc, border_sprite_short_width);
//         gl.uniform1f(flame_height_loc, short_flame_height);

//         border_short_sprite.model.set_position(0.0, 
//             resolution_size[1] - border_sprite_height);
//         border_short_sprite.model.set_rotation(-90);
//         border_short_sprite.draw(gl, flame_shader);

//         border_short_sprite.model.set_position(resolution_size[0], 
//             border_sprite_height);
//         border_short_sprite.model.set_rotation(90);
//         border_short_sprite.draw(gl, flame_shader);



//         gl.bindFramebuffer(gl.FRAMEBUFFER, corner_framebuffer);

//         gl.clear(gl.COLOR_BUFFER_BIT);

//         gl.activeTexture(gl.TEXTURE0);
//         gl.bindTexture(gl.TEXTURE_2D, noise_texture);


//         //gl.uniform2f(flame_iResolution_loc, 2 * Math.PI * corner_sprite_size / 2.0, 
//         //    border_sprite_height * 2.9);
//         gl.uniform2f(flame_iResolution_loc, resolution_size[0], 
//             resolution_size[1]);
//         gl.uniform2f(object_size_loc, corner_sprite_size, corner_sprite_size);
//         gl.uniform1f(flame_width_loc, corner_sprite_size);
//         gl.uniform1f(flame_height_loc, corner_flame_height);

//         corner_sprite.model.set_position(0.0, 0.0);
//         corner_sprite.model.set_rotation(0);
//         corner_sprite.draw(gl, flame_shader);

//         corner_sprite.model.set_position(0.0, resolution_size[1]);
//         corner_sprite.model.set_rotation(-90);
//         corner_sprite.draw(gl, flame_shader);

//         corner_sprite.model.set_position(resolution_size[0], 
//             resolution_size[1]);
//         corner_sprite.model.set_rotation(-180);
//         corner_sprite.draw(gl, flame_shader);

//         corner_sprite.model.set_position(resolution_size[0], 0.0);
//         corner_sprite.model.set_rotation(90);
//         corner_sprite.draw(gl, flame_shader);



//         gl.useProgram(texture_shader);
//         gl.bindFramebuffer(gl.FRAMEBUFFER, null);

//         gl.clear(gl.COLOR_BUFFER_BIT);

//         gl.activeTexture(gl.TEXTURE0);
//         gl.bindTexture(gl.TEXTURE_2D, border_texture);

//         gl.activeTexture(gl.TEXTURE1);
//         gl.bindTexture(gl.TEXTURE_2D, corner_texture);

//         screen_sprite.draw(gl, texture_shader);

//         console.log('REnder');

//         requestAnimationFrame(render);
//     }