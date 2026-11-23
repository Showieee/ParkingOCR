export interface PlateRectangle {
  x: number;
  y: number;
  width: number;
  height: number;
}

export interface CharacterBoxDto {
  character: string;
  confidence: number;
  x: number;
  y: number;
  width: number;
  height: number;
  isCountryCode: boolean;
}

export interface PlateValidationResult {
  isValid: boolean;
  formattedPlate: string;
  countryCode?: string | null;
  county?: string | null;
  numbers?: string | null;
  letters?: string | null;
  plateType: string;
  validationMessage?: string | null;
}

export interface OcrRecognitionResponse {
  success: boolean;
  plateText: string;
  rawText: string;
  countryCode?: string | null;
  confidence: number;
  confidencePercentage: string;
  processingTimeMs: number;
  boundingBox?: PlateRectangle | null;
  characters: CharacterBoxDto[];
  validation: PlateValidationResult;
  plateCropBase64?: string | null;
  processedImageBase64?: string | null;
  message?: string | null;
}

export interface SampleImageDto {
  id: string;
  filename: string;
  title: string;
  description: string;
  expectedPlate: string;
  hasEuroband: boolean;
  thumbnailBase64: string;
}

export interface ScanHistoryItem {
  id: string;
  timestamp: string;
  thumbnail: string;
  plateText: string;
  countryCode?: string | null;
  confidence: number;
  durationMs: number;
  success: boolean;
  fullResponse: OcrRecognitionResponse;
}
