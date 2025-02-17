let shader_settings =
{
    time_effect: 0.7,                   //скорость пламени
    sigma: 2.0,                         //сигма (SIGMA), чем больше, тем больше будет размытие вокруг каждого пикселя
    luma_min: 0.01,
    luma_min_smooth: 0.01,
    is_mirror: false,                   //Рисовать отраженное пламя
    is_reflected: true,                 //Рисовать только отраженное пламя
    is_apply_to_alpha_layer: true,      //очистить фон
    alpha_percentage: 100,              //прозрачность пламени. 100 - макс. видимость
    flame_multi_color:                  
    {
        r: 255,
        g: 0,
        b: 0,
    },                                  //цвет мультипликатор для пламени (0-255)
    logo_radius: 220.0,					//радиус лого
    logo_flame_height: 300.0,			//высота пламени
    logo_rotation_speed: 2,           //скорость поворота лого
    logo_scale_speed: 0.0,              //скорость масштабирования лого
    logo_border_rotation_speed: 20.0,    //скорость поворота пламени лого
    logo_border_scale_speed: 0.0,       //скорость масштабирования пламени лого
    max_fps: 90							//максимальное ФПС
};