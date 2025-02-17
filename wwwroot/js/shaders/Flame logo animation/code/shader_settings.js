let shader_settings =
{
    time_effect: 0.7,                           // скорость пламени
    sigma: 2.0,                                 // сигма (SIGMA), чем больше, тем больше будет размытие вокруг каждого пикселя
    luma_min: 0.01,
    luma_min_smooth: 0.01,
    is_mirror: false,                           // рисовать отраженное пламя
    is_reflected: true,                         // рисовать только отраженное пламя
    is_apply_to_alpha_layer: true,              // очистить фон
    alpha_percentage: 100,                      // прозрачность пламени. 100 - макс. видимость
    flame_multi_color:                  
    {
        r: 120,
        g: 50,
        b: 37,
    },                                          // цвет мультипликатор для пламени (0-255)
    flame_multi_color_weight: 1.0,              // 0.0 - чистый цвет, 1.0 - полное наложение изначального цвета пламени (0.0 - 1.0)
    is_use_image_average_color: true,           // использовать средний цвет лого для пламени
    is_image_on_flame: false,                   // рисовать лого на пламени

    logo_radius: 80.0,					        // радиус лого
    logo_rotation_speed: 0.0,                   // скорость поворота лого (знак влияет на направление)
    
    logo_flame_height: 80.0,			        // высота пламени
    logo_flame_rotation_speed: 0.0,             // скорость поворота пламени лого (знак влияет на направление)

    is_need_show_fps: false,                    // показывать в дебаге фпс
    max_fps: 90							        // максимальное ФПС
};