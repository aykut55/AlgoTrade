def strip_jsonc(text):
    """// yorumlarini ve kapanistan once gelen trailing virgulleri kaldirir (string icini korur)."""
    out = []
    inString = False
    escape = False
    i = 0
    n = len(text)
    while i < n:
        ch = text[i]
        if inString:
            out.append(ch)
            if escape:
                escape = False
            elif ch == "\\":
                escape = True
            elif ch == '"':
                inString = False
            i += 1
            continue
        if ch == '"':
            inString = True
            out.append(ch)
            i += 1
            continue
        if ch == "/" and i + 1 < n and text[i + 1] == "/":
            while i < n and text[i] != "\n":
                i += 1
            continue
        if ch == ",":
            j = i + 1
            while j < n and text[j] in " \t\r\n":
                j += 1
            if j < n and text[j] in "}]":
                i += 1
                continue
        out.append(ch)
        i += 1
    return "".join(out)
