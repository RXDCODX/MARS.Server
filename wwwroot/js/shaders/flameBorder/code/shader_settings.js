let shader_settings =
    {
        time_effect: 0.3,                   // скорость пламени
        flame_height: 190.0,                // высота пламени
        sigma: 1.0,                         // сигма (SIGMA), чем больше, тем больше будет размытие вокруг каждого пикселя
        luma_min: 0.01,
        luma_min_smooth: 0.01,
        is_mirror: false,                   // Рисовать отраженное пламя
        is_reflected: false,                // Рисовать только отраженное пламя
        is_apply_to_alpha_layer: true,      // очистить фон
        alpha_percentage: 100,              // прозрачность пламени. 100 - макс. видимость
        flame_multi_color:
            {
                r: 100,
                g: 0,
                b: 255,
            },                                  // цвет мультипликатор для пламени (0-255)
        corner_radius: 1,                  // радиус закругления в углах
        is_unfading: false,           		// начинать ли с разгорания
        unfading_duration: 30,				// длительность разгорания в секундах
        is_fading: false,                    // должно ли пламя со временм затухать
        fading_duration: 30,                // длительность затухания в секундах
        max_fps: 30							// максимальное ФПС
    };