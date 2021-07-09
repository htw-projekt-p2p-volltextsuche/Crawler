import re

class rede:
    def __init__(self, title, date):    
        self.title = title
        self.title_short = self.shorten_title(self.title)
        self.speaker = ""
        self.affiliation = ""
        self.date = date
        self.text = ""

    def shorten_title(self, title):
        _, short = self.remove_kategorie(title)
        short = self.assemble_subtitles(short)
        short = " ".join([self.remove_prefix(i) for i in short])
        return short

    def remove_kategorie(self, title):
        kategorie = ""
        inhalt = ""
        title_list = title.split()
        for i in range(0, len(title_list)):
            if title_list[i][-1] == ":":
                kategorie = " ".join(title_list[:i+1])
                inhalt = " ".join(title_list[i+1:])
                break
        return kategorie, inhalt

    def remove_prefix(self, title):
        startwords = ["Antrag", "Beschlussempfehlung", "Unterrichtung", "Aktuelle", "Wahlvorschlag"]
        stopwords = ["zum"]

        split = title.split()
        startpoint = 0
        stoppoint = 0
        for i in range(0, len(split)):
            if split[i] in startwords:
                for j in range(i, len(split)):
                    if split[j][-1] == ":" or split[j] in stopwords:
                        stoppoint = j
                        startpoint = i
                        break
                break
        else:
            return title

        result = " ".join(split[:startpoint]) +  " ".join(split[stoppoint+1:])
        return result


    def assemble_subtitles(self, title):
        letters = re.compile("(.)\\1+\)")
        
        tmp_list = []
        title_list = title.split(" ")
        count = 0
        while count < len(title_list):
            if letters.match(title_list[count]):
                tmp_list.append(" ".join(title_list[:count]).replace("\xa0", " "))
                title_list = title_list[count:]
                count = 0
            count += 1
        tmp_list.append(" ".join(title_list[:]).replace("\xa0", " "))
        if "" in tmp_list:
            tmp_list.remove("")
        return tmp_list

    def __str__(self):
        return "\nTitle: " + self.title + \
               "\nTitle_short: " + self.title_short + \
               "\nSpeaker: " + self.speaker + \
               "\nAffiliation: " + self.affiliation + \
               "\nDate: " + self.date + \
               "\nText: " + self.text
