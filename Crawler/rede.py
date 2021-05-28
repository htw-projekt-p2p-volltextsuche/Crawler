class rede:
    def __init__(self, title, date):
        self.title = title
        self.speaker = ""
        self.affiliation = ""
        self.date = date
        self.text = ""

    def set_affiliation(self, affiliation):
        self.affiliation = affiliation

    def set_speaker(self, speaker):
        self.speaker = speaker

    def set_text(self, text):
        self.text = text

    def __str__(self):
        return "\nTitle: " + self.title + \
               "\nSpeaker: " + self.speaker + \
               "\nAffiliation: " + self.affiliation + \
               "\nDate: " + self.date + \
               "\nText: " + self.text
