#include <SoftwareSerial.h>
#include <FastLED.h>

//#define DEBUG

#define OPCODE_HEADER_1 0xF0
#define OPCODE_HEADER_2 0xAA

#define NUM_LEDS 60
#define NUM_SPECTRUM 15
#define DATA_PIN 2
#define LED_TYPE WS2812

#define CONTROLLER_MODE_FREEFORM 0x0
#define CONTROLLER_MODE_MUSIC_SYNC 0x1
#define CONTROLLER_MODE_UNKNOWN 0xFF

// Define the array of leds
CRGB leds[NUM_LEDS];
byte spectrumData[NUM_SPECTRUM];
uint8_t hues[NUM_LEDS];

SoftwareSerial hc06(10,11);

byte controllerMode = CONTROLLER_MODE_UNKNOWN;

// Freeform mode variables
byte finishedRoutine, isIdle;
// Music sync variables
unsigned long lastDone;

void setup() {
  Serial.begin(9600);
  hc06.begin(9600);

  FastLED.addLeds<LED_TYPE, DATA_PIN, GRB>(leds, NUM_LEDS);
  //FastLED.setBrightness(84);

  for(int i = 0; i < NUM_LEDS; i++) {
    hues[i] = (i*256/NUM_LEDS - 30) % 256;
    leds[i] = CHSV(hues[i], 255, 255);
  }
  FastLED.show();

#ifdef DEBUG
  Serial.println("Finished setting up board");
#endif
}

void loop() {
  
  byte opcode = readOpCode();

#ifdef DEBUG
    Serial.print("Received Code: ");
    Serial.print(opcode);
    Serial.println("");
#endif

  // General opcodes (any mode)
  switch(opcode) {
    case 0x1:
      handlePing();
      break;
    case 0x2:
      handleHello();
      break;
    case 0x6:
      handleSetMode();
      break;
  }

  // Specific opcodes
  switch(controllerMode) {
    case CONTROLLER_MODE_FREEFORM:
      switch(opcode) {
        case 0x5:
          handleHsv();
          break;
        case 0x6:
          handleSetMode();
          break;
        case 0x7:
          finishedRoutine = true;
          break;
      }
      break;
      
    case CONTROLLER_MODE_MUSIC_SYNC:
      switch(opcode) {
        case 0x3:
          handleData();
          break;
      }
      break;
  }

  // Execute current mode routine
  switch(controllerMode) {
    case CONTROLLER_MODE_FREEFORM:
      freeformRoutine();
      break;
    case CONTROLLER_MODE_MUSIC_SYNC:
      musicSyncRoutine();
      break;
  }
  
  //Write from Serial Monitor to HC06f

#ifdef DEBUG
  if (Serial.available()) {
    hc06.write(Serial.read());
  }
#endif
}

byte readOpCode() {
  byte b;
  while(1) {
    // Search for first byte in header
    while(hc06.available()) {
      b = hc06.read();
      //Serial.print(b);
      //Serial.println("");
      if(b == OPCODE_HEADER_1) {
        goto secondCheck;
      }
    }
    if(!hc06.available()) {
      // End of stream, we did not consume any header so its safe to abort.
      return 0xFF;
    }

    //Serial.println(":D");
    
    // We consumed OPCODE_HEADER_1, we must get next 2 bytes (2nd header && opcode)
secondCheck:
    hc06.readBytes(&b, 1);
    //Serial.print(b);
    //Serial.println(" b");
    switch(b) {
      case OPCODE_HEADER_1:
        // Maybe this byte is the start of the header.
        goto secondCheck;
        break;
      case OPCODE_HEADER_2:
        // We matched all our header, return opcode.
        hc06.readBytes(&b, 1);
        return b;
      default:
        continue;
    }
  }
}

void handlePing() {
  hc06.write(0x1);
}

void handleHello() {
  hc06.write(0x2);
  uint16_t leds = NUM_LEDS;
  byte* ptr = (byte*)&leds;
  for(int i = 0; i < 2; i++) {
    hc06.write(ptr[i]); 
  }
  *ptr = NUM_SPECTRUM;
  for(int i = 0; i < 2; i++) {
    hc06.write(ptr[i]); 
  }
}

void handleData() {
  hc06.readBytes(spectrumData, NUM_SPECTRUM);
  double chunk = (double)NUM_LEDS/NUM_SPECTRUM;
  int ledId = 0;
  double maxId = 0;
  for(int i = 0; i < NUM_SPECTRUM; i++) {
    maxId += chunk;
    while(ledId <= maxId) {
      leds[ledId] = CHSV(hues[ledId], 255, spectrumData[i]);
      ledId++;
    }
  }
  FastLED.show();
  Serial.println("handle");
  hc06.write(0x3);
  lastDone = millis();
}

unsigned long lastMillis;
byte displayMode, dataMode;
uint16_t params[2];

void handleHsv() {
  lastMillis = millis();
  
  hc06.readBytes(&displayMode, 1);
  switch(displayMode) {
    case 0x1:
    case 0x2:
      params[0] = readShort();
      break;
    case 0x3:
      params[0] = readShort();
      params[1] = readShort();
      break;
  }

  hc06.readBytes(&dataMode, 1);
  switch(dataMode) {
    case 0x0:
      CHSV h = readHSV();
      fill_solid(leds, NUM_LEDS, h);
      break;
    case 0x1:
      for(int i = 0; i < NUM_LEDS; i++) {
        leds[i] = readHSV();
      }
      break;
  }

  FastLED.setBrightness(255);
  if(displayMode == 0x0) {
    FastLED.show();
  }
  else {
    finishedRoutine = false;
  }

#ifdef DEBUG
  Serial.print("handleHsv() -> ");
  Serial.print(displayMode); Serial.print(" (");
  Serial.print(params[0]); Serial.print(", "); Serial.print(params[1]); Serial.print(") ");
  Serial.print(dataMode);
  Serial.println("---");
#endif
}

void handleSetMode() {
  byte mode;
  hc06.readBytes(&mode, 1);

  // Initiate mode
  switch(mode) {
    case CONTROLLER_MODE_FREEFORM:
      finishedRoutine = true;
      isIdle = true;
      break;
    case CONTROLLER_MODE_MUSIC_SYNC:
      lastDone = millis();
      break;
    default:
#ifdef DEBUG
      Serial.print("Received unknown mode: ");
      Serial.print(mode);
      Serial.println(".");
#endif
      return;
  }

  controllerMode = mode;

  // Send controller mode ack
  hc06.write(0x4);
  hc06.write(controllerMode);
}

void freeformRoutine() {
  if(finishedRoutine) {
    if(!isIdle) {
      // send Idle message so that client doesn't have to send cancel message.
      hc06.write(0x5);
      isIdle = true;
    }
    return;
  }
  
  unsigned long elapsed = millis() - lastMillis;
  byte brightness;
  
  isIdle = false;
  switch(displayMode) {
    case 0x0:
      return;
    case 0x1:
      if(elapsed > params[0]) {
        brightness = 255;
        finishedRoutine = true;
        break;
      }
      brightness = elapsed * 255 / params[0];
      break;
    case 0x2:
      if(elapsed > params[0]) {
        brightness = 0;
        finishedRoutine = true;
        break;
      }
      brightness = 255 - elapsed * 255 / params[0];
      break;
    case 0x3:
      if(elapsed > params[0]) {
        brightness = 255;
        lastMillis += params[0];
        params[0] = params[1];
        displayMode = 0x2; // Fadeout
        break;
      }
      brightness = elapsed * 255 / params[0];
      break;
  }

  FastLED.setBrightness(brightness);
  FastLED.show();
}

void musicSyncRoutine() {
  if(millis() - lastDone > 1000) {
    // 1 sec elapsed since last done, resend done msg
    hc06.write(0x3);
    lastDone = millis();
  }
}

uint16_t readShort() {
  byte buff[2];
  hc06.readBytes(buff, 2);
  return buff[1]*0xFF + buff[0];
}

CHSV readHSV() {
  byte data[3];
  hc06.readBytes(data, 3);
  return CHSV(data[0], data[1], data[2]);
}
