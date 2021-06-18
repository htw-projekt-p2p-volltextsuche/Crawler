import sys
import rede as r
import json
import io

def reden_decoder(obj):
    if "title_short" in obj:
            print("titles already shortened")
            sys.exit(0)
    rede = r.rede(obj["title"], obj["date"])
    rede.speaker = obj["speaker"]
    rede.affiliation = obj["affiliation"]
    rede.text = obj["text"]
    return rede

if __name__ == "__main__":
        filename = sys.argv[1]

        reden = []

        with io.open(filename, "r", encoding="utf-8") as f:
            reden_obj = json.load(f)
            for i in reden_obj:
                reden.append(reden_decoder(i))

        y = [i.__dict__ for i in reden]

        with io.open(filename, "w+", encoding="utf-8") as f:
            json.dump(y, f, ensure_ascii=False, indent=" ") 
