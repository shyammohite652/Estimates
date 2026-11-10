import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  CatalogueItem, EstimateDetails, EstimateHistoryEntry, EstimateLine, PagedResult, ProjectSummary,
} from '../models';
import { API_BASE_URL } from './api-base';

/** Typed client for the estimating API. No business rules here — just the contract. */
@Injectable({ providedIn: 'root' })
export class EstimatorApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  myProjects(): Observable<ProjectSummary[]> {
    return this.http.get<ProjectSummary[]>(`${this.base}/projects`);
  }

  createEstimate(projectId: string, body: { description: string; pricingDate: string }): Observable<EstimateDetails> {
    return this.http.post<EstimateDetails>(`${this.base}/projects/${projectId}/estimates`, body);
  }

  estimate(id: string): Observable<EstimateDetails> {
    return this.http.get<EstimateDetails>(`${this.base}/estimates/${id}`);
  }

  lines(id: string, page: number, pageSize: number): Observable<PagedResult<EstimateLine>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<EstimateLine>>(`${this.base}/estimates/${id}/lines`, { params });
  }

  history(id: string): Observable<EstimateHistoryEntry[]> {
    return this.http.get<EstimateHistoryEntry[]>(`${this.base}/estimates/${id}/history`);
  }

  addOrAdjustLine(id: string, body: { catalogueItemId: string; quantity: number; markupPercent: number }): Observable<EstimateLine> {
    return this.http.post<EstimateLine>(`${this.base}/estimates/${id}/lines`, body);
  }

  updateLine(id: string, lineId: number, body: { quantity: number; markupPercent: number }): Observable<EstimateLine> {
    return this.http.put<EstimateLine>(`${this.base}/estimates/${id}/lines/${lineId}`, body);
  }

  removeLine(id: string, lineId: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/estimates/${id}/lines/${lineId}`);
  }

  submit(id: string): Observable<EstimateDetails> {
    return this.http.post<EstimateDetails>(`${this.base}/estimates/${id}/submit`, {});
  }

  approve(id: string): Observable<EstimateDetails> {
    return this.http.post<EstimateDetails>(`${this.base}/estimates/${id}/approve`, {});
  }

  reject(id: string, reason: string): Observable<EstimateDetails> {
    return this.http.post<EstimateDetails>(`${this.base}/estimates/${id}/reject`, { reason });
  }

  reopen(id: string): Observable<EstimateDetails> {
    return this.http.post<EstimateDetails>(`${this.base}/estimates/${id}/reopen`, {});
  }

  correctDescription(id: string, description: string): Observable<EstimateDetails> {
    return this.http.put<EstimateDetails>(`${this.base}/estimates/${id}/description`, { description });
  }

  catalogue(pricingDate: string, search: string): Observable<CatalogueItem[]> {
    let params = new HttpParams().set('pricingDate', pricingDate);
    if (search.trim()) params = params.set('search', search.trim());
    return this.http.get<CatalogueItem[]>(`${this.base}/catalogue`, { params });
  }
}
