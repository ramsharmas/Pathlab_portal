// JavaScript Document

//jQuery(window).scroll(function(){
    //if (jQuery(this).scrollTop() > 39) {
      // jQuery('.menu-section').addClass('frizeclass');
    //} else {
       //jQuery('.menu-section').removeClass('frizeclass');
    //}
//});


/*--
Slick Slider
------------------------*/

jQuery(document).on('ready', function() {  
  jQuery(".regular").slick({
	dots: true,
	arrows: false,
	infinite: true,
	autoplay: true,
	slidesToShow:1,
	slidesToScroll: 1
  });
});


jQuery(document).on('ready', function() {  
  jQuery(".regular-one").slick({
	dots: false,
	arrows: true,
	infinite: true,
	autoplay: true,
	slidesToShow:3,
	slidesToScroll: 1,
	responsive: [
      {
        breakpoint: 991,
        settings: {
          slidesToShow: 1,
        }
      }
	 ]
  });
});



//Tabt
jQuery('.gender-box li').click(function(){
		var index = jQuery(this).index();
		jQuery('.gender-box li').removeClass('active');
		jQuery(this).addClass('active');
		jQuery('.org-outer').hide();
		jQuery('.org-outer').eq(index).show();
		return false
	});


//Tabt - TWO
jQuery('.test-package-outer li').click(function(){
		var index = jQuery(this).index();
		jQuery('.test-package-outer li').removeClass('active');
		jQuery(this).addClass('active');
		jQuery('.test-package-details').hide();
		jQuery('.test-package-details').eq(index).show();
		return false
	});

//Staff-speech-popup
jQuery(".staff-speech").click(function(e){ 
    jQuery(".staff-speech-popup").show();
   return false;
});
jQuery('.cross').click(function() {
   jQuery(".staff-speech-popup").hide();
   return false;
});


jQuery('.accrodion-grp .accrodion').click(function(){
		var index = jQuery(this).index();
		jQuery('.accrodion-grp .accrodion').removeClass('active');
		jQuery(this).addClass('active');
		jQuery('.accrodion-content').hide();
		jQuery('.accrodion-content').eq(index).show();
		return false
	});





/*--
burger menu 
------------------------*/
jQuery(".burger-menu").click(function(e){ 
    jQuery(".menu-section").toggle();
}); 

jQuery('.close').click(function() {
   jQuery(".login-box").hide();
   return false;
});
 

/*--
Start popup
------------------------*/

jQuery(".book-test").click(function(e){ 
    jQuery(".start-popup").show();
   return false;
});

jQuery(".search-icon").click(function(e){ 
    jQuery(".searh-pop").show();
   return false;
});

jQuery(".user-icon").click(function(e){
  e.preventDefault();
  var user = localStorage.getItem('sd_user');
  window.location.href = user ? APP_ROOT + 'Patient/Portal' : APP_ROOT + 'Account/Login';
});

jQuery(".report-status").click(function(e){ 
    jQuery(".login-popup").show();
   return false;
});

jQuery(".include").click(function(e){ 
    jQuery(".include-popup").show();
   return false;
});

jQuery(".see-bro").click(function(e){ 
    jQuery(".centre-popup").show();
   return false;
});

jQuery(".book-pack").click(function(e){ 
    jQuery(".centre-popup").show();
   return false;
});

jQuery(".logout-icon").click(function(e){
  e.preventDefault();
  localStorage.removeItem('sd_user');
  sessionStorage.removeItem('pathlabUser');
  sessionStorage.removeItem('sd_cart');
  sessionStorage.removeItem('sd_collection');
  window.location.href = '/';
});

// Hide logout icon when not logged in; hide user icon when logged in
(function(){
  var loggedIn = !!localStorage.getItem('sd_user');
  jQuery('.header-icon a.logout-icon').closest('.header-icon').toggle(loggedIn);
  // optionally swap user icon label
})();

jQuery(".book-log").click(function(e){
    jQuery(".login-popup").show();
   return false;
});

jQuery(".cart-icon").click(function(e){
  e.preventDefault();
  window.location.href = APP_ROOT + 'Booking/Cart';
  return false;
});

jQuery(".video-one").click(function(e){ 
    jQuery(".video-popup").show();
   return false;
});

jQuery(".cross").click(function(){
  jQuery(".start-popup") .hide() 
   jQuery(".searh-pop").hide();
   jQuery(".login-pop").hide();
   jQuery(".centre-popup").hide();
   jQuery(".include-popup").hide(); 
   jQuery(".login-popup").hide(); 
   jQuery(".video-popup").hide(); 
}); 
 















/*--
Sign in
------------------------*/
jQuery(".show-form").click(function(){
  $(".member-form-box") .show() 
});
jQuery(".cross").click(function(){
  $(".member-form-box") .hide()  
});
 

/*--
forgot
------------------------*/
jQuery(".forgot").click(function(){
  $(".log-form") .hide()
  $(".forgot-form") .show()
});
jQuery(".cross-three").click(function(){
  $(".forgot-form") .hide()
});

/*--
Nav mega-menu: force-close on scroll
------------------------*/
// .sub-menu opens on CSS :hover. Browsers only recompute :hover on real
// pointer events, not on scroll — so if the cursor stays put over the
// trigger while the page is scrolled (mouse wheel/trackpad), the dropdown
// stays visually open and floats over the content below. Toggling display
// off then back to the CSS default on scroll forces it shut without
// blocking the next genuine hover.
jQuery(window).on('scroll', function () {
  jQuery('.menu-section .sub-menu:visible').css('display', 'none').each(function () {
    jQuery(this).css('display', '');
  });
});

 