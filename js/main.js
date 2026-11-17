(function(){
  var REPO="MobinMardi/Exoptimizer",LATEST="https://github.com/"+REPO+"/releases/latest";
  var $=function(s,r){return Array.prototype.slice.call((r||document).querySelectorAll(s))};

  /* theme toggle */
  document.getElementById("theme").addEventListener("click",function(){
    var r=document.documentElement,n=r.getAttribute("data-theme")==="light"?"dark":"light";
    r.setAttribute("data-theme",n);
    document.querySelector('meta[name="theme-color"]').content=n==="light"?"#f5f6fb":"#0e0f15";
    try{localStorage.setItem("exo-theme",n)}catch(e){}
  });

  /* latest release → every download button */
  function mb(b){return (b/1048576).toFixed(1)+" MB"}
  function apply(rel){
    var assets=(rel.assets||[]).filter(function(a){return a.browser_download_url});
    var pick=function(re){return assets.filter(function(a){return re.test(a.name)})[0]};
    var a=pick(/setup.*\.exe$|install.*\.exe$/i)||pick(/\.exe$/i)||pick(/\.msi$/i)||pick(/\.zip$/i)||assets[0];
    var ver=(rel.tag_name||"").replace(/^v/i,"");
    var url=a?a.browser_download_url:(rel.html_url||LATEST);
    $("[data-dl]").forEach(function(el){el.href=url;if(a)el.setAttribute("download","")});
    $("[data-label]").forEach(function(el){el.textContent="Download v"+ver+" for Windows"});
    var parts=["Version "+ver];
    if(a)parts.push(mb(a.size));
    if(rel.published_at)parts.push(new Date(rel.published_at).toLocaleDateString(undefined,{year:"numeric",month:"short",day:"numeric"}));
    parts.push("Windows 10 / 11");
    document.getElementById("meta").innerHTML="<b>"+parts.join("</b> · <b>").replace(/<b>(Windows 10 \/ 11)<\/b>/,"$1")+"</b>";
    document.getElementById("dl-sub").textContent="Version "+ver+(a?" · "+a.name:"")+", straight from GitHub Releases.";
  }
  var key="exo-release",cached=null;
  try{cached=JSON.parse(sessionStorage.getItem(key))}catch(e){}
  if(cached&&Date.now()-cached.t<6e5)apply(cached.d);
  else fetch("https://api.github.com/repos/"+REPO+"/releases/latest",{headers:{Accept:"application/vnd.github+json"}})
    .then(function(r){if(!r.ok)throw 0;return r.json()})
    .then(function(d){apply(d);try{sessionStorage.setItem(key,JSON.stringify({t:Date.now(),d:d}))}catch(e){}})
    .catch(function(){/* buttons already point at /releases/latest */});

  /* gentle live numbers in the preview */
  var cfg={cpu:[14,38,"%"],mem:[41,62,"%"],ping:[18,34," ms"]};
  function tick(){Object.keys(cfg).forEach(function(k){
    var c=cfg[k],v=Math.round(c[0]+Math.random()*(c[1]-c[0]));
    $('[data-v="'+k+'"]').forEach(function(e){e.textContent=v+c[2]});
    $('[data-b="'+k+'"]').forEach(function(e){e.style.width=(k==="ping"?v*2:v)+"%"});
  })}
  tick();
  if(!matchMedia("(prefers-reduced-motion: reduce)").matches)setInterval(tick,2200);
})();
