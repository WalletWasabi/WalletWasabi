function statusShow(progress, text) {
    document.getElementById("status").innerHTML = '<div class="progress" style="margin:0"><div class="progress-bar" role="progressbar" style="width:' + progress + '%;"><span><strong>' + text + '</strong></span></div></div>';
}