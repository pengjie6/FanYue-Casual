// 注意：jslib 里导出的函数会被 Unity 当作普通函数调用，此时 this 不是插件对象，
// 用 this.patterns 会抛 "Cannot set properties of undefined (setting 'patterns')"，
// 抖音小游戏里这会直接把 UnityPlugin 打死（quit: status 1）。所以统一挂在 Module 上。
var HapticPlugin = {
    _Initialize: function() {
        Module.__hapticPatterns = {};
    },

    _Play: function(duration) {
        if (typeof navigator !== 'undefined' && navigator.vibrate) {
            navigator.vibrate(duration);
        }
    },

    _RegisterPattern: function(idPtr, idLen, ptr, length) {
        if (!Module.__hapticPatterns) {
            Module.__hapticPatterns = {};
        }

        var id = UTF8ToString(idPtr, idLen);

        var pattern = [];
        for (var i = 0; i < length; i++) {
            pattern.push(HEAP32[(ptr >> 2) + i]);
        }

        Module.__hapticPatterns[id] = pattern;
    },

    _PlayPattern: function(idPtr, idLen) {
        var id = UTF8ToString(idPtr, idLen);

        var pattern = Module.__hapticPatterns ? Module.__hapticPatterns[id] : null;
        if (pattern) {
            if (typeof navigator !== 'undefined' && navigator.vibrate) {
                navigator.vibrate(pattern);
            }
        } else {
            console.error("Haptic pattern with ID '" + id + "' can't be found!");
        }
    }
};

mergeInto(LibraryManager.library, HapticPlugin);
