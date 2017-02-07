var mobile_menu_initialized = false;
var toggle_initialized = false;
var mobile_menu_visible = 0;

var lbd = {
    misc: {
        navbar_menu_visible: 0
    },

    initRightMenu: debounce(function () {
        $sidebar_wrapper = $('.sidebar-wrapper');

        if (!mobile_menu_initialized) {
            $navbar = $('nav').find('.navbar-collapse').first().clone(true);

            nav_content = '';
            mobile_menu_content = '';

            $navbar.children('ul').each(function () {

                content_buff = $(this).html();
                nav_content = nav_content + content_buff;
            });

            nav_content = '<ul class="nav nav-mobile-menu">' + nav_content + '</ul>';

            $sidebar_nav = $sidebar_wrapper.find(' > .nav');

            // insert the navbar form before the sidebar list
            $nav_content = $(nav_content);
            $nav_content.insertBefore($sidebar_nav);

            $(".sidebar-wrapper .dropdown .dropdown-menu > li > a").click(function (event) {
                event.stopPropagation();

            });

            mobile_menu_initialized = true;
        } else {
            if ($(window).width() > 991) {
                // reset all the additions that we made for the sidebar wrapper only if the screen is bigger than 991px
                $sidebar_wrapper.find('.navbar-form').remove();
                $sidebar_wrapper.find('.nav-mobile-menu').remove();

                mobile_menu_initialized = false;
            }
        }

        if (!toggle_initialized) {
            $toggle = $('.navbar-toggle');
            $layer = $('.close-layer');

            $toggle.click(function () {

                if (mobile_menu_visible == 1) {
                    $('html').removeClass('nav-open');
                    $layer.removeClass('visible');

                    setTimeout(function () {
                        $toggle.removeClass('toggled');
                    }, 400);

                    setTimeout(function () {
                        $('.close-layer').remove();
                    }, 100);

                    mobile_menu_visible = 0;
                } else {
                    setTimeout(function () {
                        $toggle.addClass('toggled');
                    }, 430);

                    $layer = $('<div class="close-layer"/>');
                    $layer.appendTo(".wrapper");

                    setTimeout(function () {
                        $layer.addClass('visible');
                    }, 100);

                    $('.close-layer').on("click", function () {
                        $toggle = $('.navbar-toggle');
                        $('html').removeClass('nav-open');

                        $layer.removeClass('visible');

                        setTimeout(function () {
                            $('.close-layer').remove();
                            $toggle.removeClass('toggled');
                        }, 370);

                        mobile_menu_visible = 0;
                    });

                    $('html').addClass('nav-open');
                    mobile_menu_visible = 1;

                }

            });

            toggle_initialized = true;
        }
    }, 200)
}

$(document).ready(function () {
    window_width = $(window).width();
    $sidebar = $('.sidebar');

    if (window_width <= 991) {
        lbd.initRightMenu();
    }
});

$(window).resize(function () {
    if ($sidebar.length != 0) {
        lbd.initRightMenu();
    }
});

function debounce(func, wait, immediate) {
    var timeout;
    return function () {
        var context = this, args = arguments;
        clearTimeout(timeout);
        timeout = setTimeout(function () {
            timeout = null;
            if (!immediate) func.apply(context, args);
        }, wait);
        if (immediate && !timeout) func.apply(context, args);
    };
};
