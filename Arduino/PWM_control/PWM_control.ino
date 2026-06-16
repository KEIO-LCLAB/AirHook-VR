const int mosfetPins[4] = {6, 9, 10, 11};

void setup() {
  Serial.begin(9600);
  while(!Serial);
  for (int i = 0; i < 4; i++) {
    pinMode(mosfetPins[i], OUTPUT);
    //analogWrite(mosfetPins[i], 0);
  }
}

void loop() {
  if (Serial.available() >= 5) {  
    if (Serial.read() == 0x01) {  // 1101 or COM3
      for (int i = 0; i < 4; i++) {
        analogWrite(mosfetPins[i], Serial.read());
      }
    }
  }
}