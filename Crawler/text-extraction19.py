import sys
import xml.etree.ElementTree as ET
import rede as r
import json
import io

def extract_name_of_redner(node):
    name = ""
    titel = node.find(".//titel")    
    vorname = node.find(".//vorname")
    nachname = node.find(".//nachname")
    fraktion = node.find(".//fraktion")
    rolle = node.find(".//rolle_lang")

    if titel is not None:
        name += titel.text + " "

    if vorname is not None:
        name += vorname.text + " "
        
    if nachname is not None:
        name += nachname.text

    if fraktion is not None:
        name += " (" + fraktion.text + ")"

    if rolle is not None:
        name += ", " + rolle.text

    return name

def get_inhaltspunkte(dokument):
    result = []
    tmp =  []

    blocks = dokument.find(".//inhaltsverzeichnis")

    for idx, i in enumerate(blocks):
        if i.tag == "ivz-block":
            tmp.append(i)
        elif i.tag == "ivz-eintrag" and i[0].text == "in Verbindung mit":
            tmp.append("+")


    to_merge = []
    for idx, i in enumerate(tmp):
        if i == "+":
            continue
        elif idx+1 < len(tmp) and tmp[idx+1] == "+":
            to_merge.append(i)
        elif i.findall(".//redner"):
            to_merge.append(i)
            result.append(to_merge)
            to_merge = []
        else:
            continue
    return result

def get_inhaltstitel(inhaltspunkt):
    result = inhaltspunkt.find("./ivz-block-titel").text

    for i in inhaltspunkt.findall("./ivz-eintrag"):
        if len(i) == 1:
            result += " " + i[0].text
        
    return result

def get_reden(inhaltspunkt, datum):
    result = dict()

    reden = []

    ipunkt = inhaltspunkt[-1]

    if len(inhaltspunkt) == 1:
        title = get_inhaltstitel(ipunkt)
        title_long = title
    else:
        title = get_inhaltstitel(inhaltspunkt[0])
        title_long = " in Verbindung mit ".join([get_inhaltstitel(i) for i in inhaltspunkt])

    for punkt in inhaltspunkt:
        for i in punkt.findall("./ivz-eintrag"):
            if len(i) >= 2 and len(i[0]) == 1:
                reden.append(i)

    for i in reden:
        rede_ids = i.findall(".//xref")

        if rede_ids:
            for rede_id in rede_ids:
                rid = rede_id.get("rid")
                rede = r.rede(title, datum)
                rede.title = title_long
                result[rid] = rede
    
    return result

def get_text(rede):
    text = ""
    for p in rede:
        if p.text is not None:
            text += p.text + " "

        if "klasse" in p.attrib and p.attrib["klasse"] == "redner":
            text += extract_name_of_redner(p) + ": "

    text = text.replace("\n", " ")
    text = text.replace("\t", "")

    return text

def get_text_und_redner(rede):
    rid = rede.attrib["id"]
    redner = extract_name_of_redner(rede)
    affiliation = rede.find(".//fraktion")
    if affiliation is None:
        affiliation = rede.find(".//rolle_lang")
    affiliation = affiliation.text
    text = get_text(rede)

    return rid, redner, affiliation, text

def fill_reden(reden, dokument):
    sitzung = dokument.find("./sitzungsverlauf")
    ordnungspunkte = sitzung.findall("./tagesordnungspunkt")
    texte = []

    for o in ordnungspunkte:
        texte += o.findall("./rede")

    for t in texte:
        rid, redner, affiliation, text = get_text_und_redner(t)
        if rid not in reden:
            reden[rid] = r.rede("", datum)
        reden[rid].speaker=redner
        reden[rid].affiliation = affiliation
        reden[rid].text = text

def convert_datum(datum):
    x = datum.split(".")
    return "-".join(x[::-1])

filename = sys.argv[1]

print("opening   " + filename)
tree = ET.parse(filename)
root = tree.getroot()
datum = convert_datum(root.attrib["sitzung-datum"])

inhaltspunkte = get_inhaltspunkte(root)
reden = dict()

for i in inhaltspunkte:
    reden = {**reden, **get_reden(i, datum)}

fill_reden(reden, root)
x = list(reden.values())

y = [i.__dict__ for i in x]

with io.open(filename[:-4] + ".json", "w+", encoding="utf-8") as f:
    json.dump(y, f, ensure_ascii=False, indent=" ")

