// Frida JS: Dump all readable memory regions as strings
Process.enumerateRangesSync({protection: 'r--', coalesce: true}).forEach(function(range) {
    try {
        var bytes = Memory.readByteArray(ptr(range.base), range.size);
        var str = bytes ? bytes.readUtf8String() : '';
        if (str && str.length > 10) send(str);
    } catch (e) {}
});
