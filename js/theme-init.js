/* apply saved / system theme before paint */
(function(){var t;try{t=localStorage.getItem("exo-theme")}catch(e){}
if(!t)t=matchMedia("(prefers-color-scheme: light)").matches?"light":"dark";
document.documentElement.setAttribute("data-theme",t)})();
